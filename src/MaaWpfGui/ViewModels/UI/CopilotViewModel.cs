// <copyright file="CopilotViewModel.cs" company="MaaAssistantArknights">
// Part of the MaaWpfGui project, maintained by the MaaAssistantArknights team (Maa Team)
// Copyright (C) 2021-2025 MaaAssistantArknights Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.0 only as published by
// the Free Software Foundation, either version 3 of the License, or
// any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JetBrains.Annotations;
using MaaWpfGui.Configuration.Factory;
using MaaWpfGui.Constants;
using MaaWpfGui.Helper;
using MaaWpfGui.Main;
using MaaWpfGui.Models;
using MaaWpfGui.Models.AsstTasks;
using MaaWpfGui.Models.Copilot;
using MaaWpfGui.Services;
using MaaWpfGui.States;
using MaaWpfGui.Utilities;
using MaaWpfGui.Utilities.ValueType;
using MaaWpfGui.ViewModels.Items;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Stylet;
using static MaaWpfGui.Helper.CopilotHelper;
using static MaaWpfGui.Helper.PathsHelper;
using static MaaWpfGui.Models.AsstTasks.AsstCopilotTask;
using DataFormats = System.Windows.Forms.DataFormats;
using Task = System.Threading.Tasks.Task;

namespace MaaWpfGui.ViewModels.UI;

/// <summary>
/// The view model of copilot.
/// </summary>
// 閫氳繃 container.Get<CopilotViewModel>(); 瀹炰緥鍖栨垨鑾峰彇瀹炰緥
// ReSharper disable once ClassNeverInstantiated.Global
public partial class CopilotViewModel : Screen
{
    private readonly RunningState _runningState;
    private static readonly ILogger _logger = Log.ForContext<CopilotViewModel>();
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly List<int> _copilotIdList = []; // 鐢ㄤ簬淇濆瓨浣滀笟鍒楄〃涓殑浣滀笟鐨処d锛屽浜庡悓涓€涓綔涓氾紝鍙湁閮芥墽琛屾垚鍔熸墠鐐硅禐
    private readonly List<int> _recentlyRatedCopilotId = []; // TODO: 鍙兘鑰冭檻鍔犱釜鎸佷箙鍖?
    private AsstTaskType _taskType = AsstTaskType.Copilot;
    private readonly Dictionary<string, string> _copilotJsonPathMap = []; // 涓嬫媺妗嗕笌瀹為檯浣滀笟 json 妗ｆ璺緞瀵圭収琛?

    /// <summary>
    /// 缂撳瓨鐨勫凡瑙ｆ瀽浣滀笟锛岄潪鍗虫椂娣诲姞鐨勪綔涓氫細浣跨敤璇ョ紦瀛?
    /// </summary>
    private CopilotBase? _copilotCache;
    private const string CopilotIdPrefix = "maa://"; // TODO: 浣滀笟绔欒縼绉诲畬鎴愬悗鍒犻櫎 maa:// 鏃ф牸寮忔敮鎸?
    private const string CopilotNewIdPrefix = "prts://"; // 鏂版牸寮忓墠缂€锛宲rts://12345 涓轰綔涓氾紝prts://s12345 涓轰綔涓氶泦
    private const string CopilotNewSetIdPrefix = "prts://s"; // 鏂版牸寮忎綔涓氶泦鍓嶇紑
    private static readonly string TempCopilotFile = Path.Combine(CacheDir, "_temp_copilot.json");

    // VideoRecognition 宸蹭笉鏀寔锛氫粎淇濈暀 json 浣滀笟
    private static readonly string[] _supportExt = [".json"];
    private static readonly string CopilotJsonDir = Path.Combine(ConfigDir, "copilot");
    private const string StageNameRegex = @"(?:[a-z]{0,3})(?:\d{0,2})-(?:(?:A|B|C|D|EX|S|TR|MO)-?)?(?:\d{1,2})";
    private const string InvalidStageNameChars = @"[:',\.\(\)\|\[\]\?，。【】｛｝；：]"; // 无效字符

    [GeneratedRegex(InvalidStageNameChars)]
    private static partial Regex InvalidStageNameRegex();

    [GeneratedRegex(@"^(act\d+(side|mini)|a00\d+)_")]
    private static partial Regex SideStoryStageIdRegex();

    [GeneratedRegex(@"^(main|sub|tough|hard)_")]
    private static partial Regex MainStageIdRegex();

    /// <summary>
    /// Gets the view models of log items.
    /// </summary>
    public ObservableCollection<LogItemViewModel> LogItemViewModels { get; } = [];

    /// <summary>
    /// Gets the file items for TreeView.
    /// </summary>
    public ObservableCollection<CopilotFileItem> FileItems { get; } = [];

    /// <summary>
    /// Gets or private sets the view models of Copilot items.
    /// </summary>
    public ObservableCollection<CopilotItemViewModel> CopilotItemViewModels { get; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CopilotViewModel"/> class.
    /// </summary>
    public CopilotViewModel()
    {
        PropertyDependsOnUtility.InitializePropertyDependencies(this);
        DisplayName = LocalizationHelper.GetString("Copilot");
        AddLog(LocalizationHelper.GetString("CopilotTip"), showTime: false);
        _runningState = RunningState.Instance;
        _runningState.StateChanged += (_, e) => {
            Idle = e.NewState.Idle;
            Inited = e.NewState.Inited;
            Stopping = e.NewState.Stopping;
        };
        LocalizationHelper.LanguageChanged += () => {
            DisplayName = LocalizationHelper.GetString("Copilot");
            SupportUnitUsageList.RefreshLocalization();
            ClearLog();
        };
        UserAdditionalItems.CollectionChanged += (_, _) => {
            NotifyOfPropertyChange(nameof(UserAdditionalGridHeight));
            NotifyOfPropertyChange(nameof(UserAdditionalPopupVerticalOffset));
        };

        var list = ConfigFactory.CurrentConfig.Copilot.TaskList;
        for (int i = 0; i < list.Count; i++)
        {
            CopilotItemViewModels.Add(list[i]);
        }

        CopilotItemViewModels.CollectionChanged += (_, e) => {
            _logger.Information("Copilot item collection changed: {Action}", e.Action);
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
            {
                for (int i = 0; i < CopilotItemViewModels.Count; i++)
                {
                    CopilotItemViewModels[i].Index = i;
                }

                SaveCopilotTask();
            }
        };
    }

    #region UI缁戝畾鍙婃搷浣?

    #region Log

    /// <summary>
    /// Adds log.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="color">The font color.</param>
    /// <param name="weight">The font weight.</param>
    /// <param name="showTime">Whether show time.</param>
    public void AddLog(string? content, string color = UiLogColor.Trace, string weight = "Regular", bool showTime = true)
    {
        // Copilot 鑷姩鎴樻枟鏈熼棿涔熶細鍚姩鍋滄粸璁℃椂鍣紙Start 閫氳繃 SetIdle(false) 杩涘叆杩愯鎬侊級锛?
        // 杩欓噷鐨勬棩蹇楀悓鏍峰睘浜?鏈夎緭鍑烘椿鍔?锛岄渶瑕侀噸缃鏃跺櫒锛屽惁鍒欎細璇姤浠诲姟鍗′綇銆?
        RunningState.Instance.NotifyOutputActivity();

        if (string.IsNullOrEmpty(content))
        {
            return;
        }
        Execute.OnUIThread(() => {
            LogItemViewModels.Add(new LogItemViewModel(content, color, weight, "HH':'mm':'ss", showTime: showTime));
            if (showTime)
            {
                switch (color)
                {
                    case UiLogColor.Error:
                        _logger.Error("{Content}", content);
                        break;
                    case UiLogColor.Warning:
                        _logger.Warning("{Content}", content);
                        break;
                    default:
                        _logger.Information("{Content}", content);
                        break;
                }
            }
        });

        // LogItemViewModels.Insert(0, new LogItemViewModel(time + content, color, weight));
    }

    /// <summary>
    /// Clears log.
    /// </summary>
    private void ClearLog()
    {
        Execute.OnUIThread(() => {
            foreach (var log in LogItemViewModels)
            {
                if (log.ToolTip is ToolTip t)
                {
                    t.IsOpen = false;
                    t.Content = null;
                    ToolTipService.SetPlacementTarget(t, null);
                    t.PlacementTarget = null;
                }
            }

            LogItemViewModels.Clear();
            AddLog(LocalizationHelper.GetString("CopilotTip"), showTime: false);
        });
    }

    #endregion Log

    #region 灞炴€?

    /// <summary>
    /// Gets a value indicating whether it is idle.
    /// </summary>
    public bool Idle { get => field; private set => SetAndNotify(ref field, value); }

    public bool Inited { get => field; set => SetAndNotify(ref field, value); }

    public bool Stopping { get => field; set => SetAndNotify(ref field, value); }

    /// <summary>
    /// Gets or sets a value indicating whether the start button is enabled.
    /// </summary>
    public bool StartEnabled { get => field; set => SetAndNotify(ref field, value); } = true;

    private int _copilotTabIndex = 0;

    /// <summary>
    /// Gets or sets 浣滀笟绫诲瀷锛?锛氫富绾?鏁呬簨闆?SS 1锛氫繚鍏ㄦ淳椹?2锛氭倴璁烘ā鎷?3锛氬叾浠栨椿鍔?
    /// </summary>
    public int CopilotTabIndex
    {
        get => _copilotTabIndex;
        set {
            if (!Idle)
            {
                return;
            }

            if (value == 1 || value == 3)
            {
                UseCopilotList = false;
            }

            SetAndNotify(ref _copilotTabIndex, value);
        }
    }

    private string _displayFilename = string.Empty;

    /// <summary>
    /// Gets or sets the display filename (relative path).
    /// </summary>
    public string DisplayFilename
    {
        get => _displayFilename;
        set {
            SetAndNotify(ref _displayFilename, value);
            if (string.IsNullOrEmpty(value))
            {
                Filename = string.Empty;
                return;
            }

            var copilotRoot = Path.Combine(ResourceDir, "copilot");
            var fullPath = Path.IsPathRooted(value) ? value : Path.Combine(copilotRoot, value);

            /* 绁炵浠ｇ爜锛堜綔涓氱珯 ID锛夛紝浜ょ粰 FileName 澶勭悊 */
            if (IsCopilotCode(value))
            {
                Filename = value;
            }
            /* 鐩稿/缁濆璺緞 */
            else if (File.Exists(fullPath))
            {
                Filename = fullPath;
            }
            /* copilot 鏂囦欢澶逛笅鐨勬枃浠跺悕 */
            else if (_copilotJsonPathMap.TryGetValue(Path.GetFileName(value), out var mappedPath))
            {
                Filename = mappedPath;
            }
            /* maybe 鏄叾浠栫绉樹唬鐮侊紝浜ょ粰 FileName 澶勭悊 */
            else
            {
                Filename = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the filename.
    /// </summary>
    public string Filename
    {
        get => field;
        set {
            var processedValue = ProcessFilePath(value);
            SetAndNotify(ref field, processedValue);
            UpdateDisplayFilename(processedValue);
            ClearLog();
            UpdateCopilotUrl(processedValue);
            _ = UpdateFilename(processedValue);
        }
    } = string.Empty;

    private string ProcessFilePath(string value)
    {
        // 绁炵浠ｇ爜涓嶆寜鏂囦欢璺緞澶勭悊锛屽師鏍烽€忎紶
        if (string.IsNullOrWhiteSpace(value) || IsCopilotCode(value) || File.Exists(value))
        {
            return value;
        }

        // 浠庡鐓ц〃鍙栧緱瀹屾暣 json 妗ｆ璺緞
        if (_copilotJsonPathMap.TryGetValue(value, out var fullPath))
        {
            return fullPath;
        }

        var resourceFile = Path.Combine(ResourceDir, "copilot", Path.GetFileName(value));
        return File.Exists(resourceFile) ? resourceFile : value;
    }

    private void UpdateDisplayFilename(string filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            _displayFilename = string.Empty;
        }
        else
        {
            var copilotRoot = Path.Combine(ResourceDir, "copilot");
            _displayFilename = filename.StartsWith(copilotRoot, StringComparison.OrdinalIgnoreCase)
                ? Path.GetRelativePath(copilotRoot, filename)
                : filename;
        }
        NotifyOfPropertyChange(nameof(DisplayFilename));
    }

    private void UpdateCopilotUrl(string filename)
    {
        CopilotUrl = string.IsNullOrWhiteSpace(filename) ? CopilotUiUrl : CopilotUrl;
    }

    /// <summary>
    /// 鍒ゆ柇杈撳叆鏄惁涓轰綔涓氱珯绁炵浠ｇ爜锛坢aa://銆乸rts://銆乸rts://s 鍓嶇紑銆乻12345 鎴栫函鏁板瓧锛?
    /// </summary>
    private static bool IsCopilotCode(string value)
    {
        return TryParseCopilotCode(value, out _, out _);
    }


    /// <summary>
    /// 浣滀笟绔欎唬鐮佺被鍨?
    /// </summary>
    private enum CopilotCodeType
    {
        /// <summary>涓嶆槸浣滀笟绔欎唬鐮?/summary>
        None,

        /// <summary>鍗曚釜浣滀笟</summary>
        Copilot,

        /// <summary>浣滀笟闆?/summary>
        CopilotSet,
    }

    /// <summary>
    /// 瑙ｆ瀽浣滀笟绔欎唬鐮侊紝璇嗗埆鎵€鏈夊凡鐭ユ牸寮忓苟鎻愬彇鏁板瓧 ID
    /// </summary>
    /// <param name="input">鍘熷杈撳叆锛坢aa://12345銆乸rts://12345銆乸rts://s12345銆乻12345銆?2345锛?/param>
    /// <param name="type">瑙ｆ瀽鍑虹殑绫诲瀷锛沵aa:// 鍜岀函鏁板瓧榛樿涓?Copilot锛堟寜閽笂涓嬫枃鍙鐩栵級</param>
    /// <param name="id">鎻愬彇鐨勬暟瀛?ID</param>
    /// <returns>鏄惁鎴愬姛瑙ｆ瀽</returns>
    private static bool TryParseCopilotCode(string input, out CopilotCodeType type, out int id)
    {
        type = CopilotCodeType.None;
        id = 0;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // 甯﹀墠缂€鐨勬牸寮忥紙浠庨暱鍒扮煭鍖归厤锛岄伩鍏?prts://s 琚?prts:// 鎶㈠厛锛?
        if (input.StartsWith(CopilotNewSetIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            type = CopilotCodeType.CopilotSet;
            return int.TryParse(input[CopilotNewSetIdPrefix.Length..], out id);
        }

        if (input.StartsWith(CopilotNewIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            type = CopilotCodeType.Copilot;
            return int.TryParse(input[CopilotNewIdPrefix.Length..], out id);
        }

// TODO: 浣滀笟绔欒縼绉诲畬鎴愬悗鍒犻櫎 maa:// 鏃ф牸寮忓垎鏀?
        if (input.StartsWith(CopilotIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // maa:// 鏃ф牸寮忥紝榛樿褰撳崟涓綔涓氾紙鎸夐挳涓婁笅鏂囧彲瑕嗙洊涓轰綔涓氶泦锛?
            type = CopilotCodeType.Copilot;
            return int.TryParse(input[CopilotIdPrefix.Length..], out id);
        }

        // s12345 鏍煎紡浣滀笟闆?
        if (input.Length > 1 && (input[0] is 's' or 'S') && int.TryParse(input[1..], out id))
        {
            type = CopilotCodeType.CopilotSet;
            return true;
        }

        // 绾暟瀛楋紝榛樿褰撳崟涓綔涓?
        if (int.TryParse(input, out id))
        {
            type = CopilotCodeType.Copilot;
            return true;
        }

        return false;
    }

    private bool _form;

    /// <summary>
    /// Gets or sets a value indicating whether to use auto-formation.
    /// </summary>
    [PropertyDependsOn(nameof(CopilotTabIndex))]
    public bool Form
    {
        get {
            // Tab=1/2 涓嶆敮鎸佽嚜鍔ㄧ紪闃燂紝鏍规嵁 CopilotTabIndex 缁煎悎鍒ゆ柇杩斿洖鍊?
            if (CopilotTabIndex is 1 or 2)
            {
                return false;
            }
            return _form;
        }
        set => SetAndNotify(ref _form, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to use auto-formation.
    /// </summary>
    public bool AddTrust { get => field; set => SetAndNotify(ref field, value); }

    /// <summary>
    /// Gets or sets a value indicating whether to use auto-formation.
    /// </summary>
    public bool IgnoreRequirements { get => field; set => SetAndNotify(ref field, value); }

    /// <summary>
    /// Gets or sets a value indicating whether 鐪熸鏈夊共鍛樿蹇界暐浜嗚姹?
    /// </summary>
    public bool HasRequirementIgnored { get; set; } = false;

    public int CurrentCopilotId { get; set; } = -1;

    public bool UseSanityPotion { get => field; set => SetAndNotify(ref field, value); }

    /// <summary>
    /// Gets or sets a value indicating whether to use auto-formation.
    /// </summary>
    public bool AddUserAdditional
    {
        get; set {
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Copilot.EnableUserAdditional = value;
        }
    } = ConfigFactory.CurrentConfig.Copilot.EnableUserAdditional;

    /// <summary>
    /// Gets or sets a value indicating whether to use auto-formation.
    /// </summary>
    public List<UserAdditional> UserAdditional
    {
        get; set {
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Copilot.UserAdditional = value;
        }
    } = ConfigFactory.CurrentConfig.Copilot.UserAdditional;

    [PropertyDependsOn(nameof(UserAdditional))]
    public string UserAdditionalPrettyJson
    {
        get {
            if (UserAdditional.Count == 0)
            {
                return string.Empty;
            }

            return JArray.FromObject(UserAdditional).ToString(Formatting.None).Replace("},", "},\n");
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the UserAdditional popup is open.
    /// </summary>
    public bool IsUserAdditionalPopupOpen { get; set => SetAndNotify(ref field, value); }

    /// <summary>
    /// Gets the view models of UserAdditional items.
    /// </summary>
    public ObservableCollection<UserAdditionalItemViewModel> UserAdditionalItems { get; } = [];

    private const int MaxVisibleUserAdditionalRows = 7;

    public double UserAdditionalRowHeight => 40;

    public double UserAdditionalGridMaxHeight => 350;

    public double UserAdditionalGridHeight
    {
        get {
            var rows = Math.Clamp(UserAdditionalItems.Count, 1, MaxVisibleUserAdditionalRows);
            return UserAdditionalGridMaxHeight - ((MaxVisibleUserAdditionalRows - rows) * UserAdditionalRowHeight);
        }
    }

    public double UserAdditionalPopupVerticalOffset => (UserAdditionalGridHeight - UserAdditionalGridMaxHeight) / 2;

    /// <summary>
    /// Opens the UserAdditional popup for editing.
    /// </summary>
    public void OpenUserAdditionalPopup()
    {
        // 娓呯┖鍒楄〃
        UserAdditionalItems.Clear();
        foreach (var op in UserAdditional)
        {
            if (string.IsNullOrWhiteSpace(op.Name))
            {
                continue;
            }

            var item = new UserAdditionalItemViewModel {
                Name = op.Name,
                Skill = Math.Clamp(op.Skill, 0, 3),
                Module = op.Module,
            };
            UserAdditionalItems.Add(item);
        }

        // 濡傛灉鍒楄〃涓虹┖锛屾坊鍔犱竴琛岀┖琛?
        if (UserAdditionalItems.Count == 0)
        {
            var newItem = new UserAdditionalItemViewModel { Name = string.Empty, Skill = 0, Module = 0 };
            UserAdditionalItems.Add(newItem);
        }

        IsUserAdditionalPopupOpen = true;
    }

    /// <summary>
    /// Saves the UserAdditional value from the popup.
    /// </summary>
    public void SaveUserAdditional()
    {
        // 灏嗗垪琛ㄨ浆鎹负 UserAdditional 骞跺簭鍒楀寲涓?JSON
        var list = new List<UserAdditional>();
        foreach (var item in UserAdditionalItems)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                continue; // 璺宠繃绌鸿
            }

            list.Add(new UserAdditional {
                Name = item.Name.Trim(),
                Skill = Math.Clamp(item.Skill, 0, 3),
                Module = item.Module,
            });
        }

        UserAdditional = list;
        IsUserAdditionalPopupOpen = false;
    }

    /// <summary>
    /// Cancels editing UserAdditional and closes the popup.
    /// </summary>
    public void CancelUserAdditionalEdit()
    {
        IsUserAdditionalPopupOpen = false;
    }

    /// <summary>
    /// Gets a value indicating whether can add a new UserAdditional item.
    /// </summary>
    public bool CanAddUserAdditionalItem => UserAdditionalItems.All(item => !string.IsNullOrWhiteSpace(item.Name));

    /// <summary>
    /// Adds a new UserAdditional item.
    /// </summary>
    public void AddUserAdditionalItem()
    {
        if (!CanAddUserAdditionalItem)
        {
            return;
        }

        var newItem = new UserAdditionalItemViewModel { Name = string.Empty, Skill = 0, Module = 0 };
        UserAdditionalItems.Add(newItem);
    }

    /// <summary>
    /// Removes a UserAdditional item.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    public void RemoveUserAdditionalItem(UserAdditionalItemViewModel item)
    {
        if (item != null && UserAdditionalItems.Contains(item))
        {
            UserAdditionalItems.Remove(item);
        }
    }

    public static Dictionary<string, int> ModuleMapping { get; } = new()
    {
        { LocalizationHelper.GetString("CopilotWithoutModule"), 0 },
        { "蠂", 1 },
        { "纬", 2 },
        { "伪", 3 },
        { "螖", 4 },
    };

    public class UserAdditionalItemViewModel : PropertyChangedBase
    {
        private string _name = string.Empty;

        /// <summary>
        /// Gets or sets the operator name.
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetAndNotify(ref _name, value);
        }

        private int _skill;

        /// <summary>
        /// Gets or sets the skill number.
        /// </summary>
        public int Skill
        {
            get => _skill;
            set => SetAndNotify(ref _skill, value);
        }

        private int _module;

        /// <summary>
        /// Gets or sets the module number.
        /// -1: 涓嶅垏鎹㈡ā缁?/ 鏃犺姹? 0: 涓嶄娇鐢ㄦā缁? 1-4: 涓嶅悓妯＄粍
        /// </summary>
        public int Module
        {
            get => _module;
            set => SetAndNotify(ref _module, value);
        }
    }

    private bool _useFormation;

    public bool UseFormation { get => _useFormation; set => SetAndNotify(ref _useFormation, value); }

    public List<GenericCombinedData<int>> FormationSelectList { get; } =
    [
        new() { Display = "1", Value = 1 },
        new() { Display = "2", Value = 2 },
        new() { Display = "3", Value = 3 },
        new() { Display = "4", Value = 4 },
    ];

    public int FormationIndex
    {
        get; set {
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Copilot.SelectFormation = value;
        }
    } = ConfigFactory.CurrentConfig.Copilot.SelectFormation;

    public bool UseSupportUnitUsage { get; set => SetAndNotify(ref field, value); }

    public CopilotSupportMode SupportUnitUsage
    {
        get; set {
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Copilot.SupportMode = value;
        }
    } = ConfigFactory.CurrentConfig.Copilot.SupportMode;

    public LocalizedObservableList<CopilotSupportMode> SupportUnitUsageList { get; } = new(
        (CopilotSupportMode.WhenNeeded, "SupportUnitUsage.WhenNeeded"),
        (CopilotSupportMode.Random, "SupportUnitUsage.Random"));

    public enum CopilotSupportMode
    {
        /// <summary>浠呰ˉ鍏呭繀瑕?/summary>
        WhenNeeded = 1,

        /// <summary>闅忔満鍔犱竴涓? 鍒蜂俊鐢ㄧ偣鐢?/summary>
        Random = 3,
    }

    private bool _useCopilotList;

    /// <summary>
    /// Gets or sets a value indicating whether 鑷姩缂栭槦.
    /// </summary>
    [PropertyDependsOn(nameof(CopilotTabIndex))]
    public bool UseCopilotList
    {
        get => _useCopilotList;
        set {
            if (value)
            {
                // _taskType 搴旇鍙敱閫夋嫨鐨勪綔涓氭枃浠跺喅瀹氾紝涓嶅湪姝ゅ己鍒朵慨鏀?
                Form = true;
            }

            SetAndNotify(ref _useCopilotList, value);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to use auto-formation.
    /// </summary>
    private string? _copilotTaskName = string.Empty;

    public string? CopilotTaskName
    {
        get => _copilotTaskName;
        set {
            value = InvalidStageNameRegex().Replace(value ?? string.Empty, string.Empty).Trim();
            SetAndNotify(ref _copilotTaskName, value);
        }
    }

    public bool Loop { get; set; }

    public int LoopTimes
    {
        get; set {
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Copilot.LoopTimes = value;
        }
    } = ConfigFactory.CurrentConfig.Copilot.LoopTimes;

    private const string CopilotUiUrl = MaaUrls.PrtsPlus;

    private string _copilotUrl = CopilotUiUrl;

    /// <summary>
    /// Gets or private sets the copilot URL.
    /// </summary>
    public string CopilotUrl
    {
        get => _copilotUrl;
        private set {
            SetAndNotify(ref _copilotUrl, value);
        }
    }

    private string _videoUrl = string.Empty;

    /// <summary>
    /// Gets or private sets the video URL.
    /// </summary>
    public string VideoUrl
    {
        get => _videoUrl;
        private set => SetAndNotify(ref _videoUrl, value);
    }

    /// <summary>
    /// Gets a value indicating whether there is a video URL.
    /// </summary>
    [PropertyDependsOn(nameof(VideoUrl))]
    public bool HasVideoUrl => !string.IsNullOrEmpty(VideoUrl);

    private const string MapUiUrl = MaaUrls.MapPrts;

    private string _mapUrl = MapUiUrl;

    public string MapUrl
    {
        get => _mapUrl;
        private set => SetAndNotify(ref _mapUrl, value);
    }

    private bool _couldLikeWebJson;

    public bool CouldLikeWebJson
    {
        get => _couldLikeWebJson;
        set => SetAndNotify(ref _couldLikeWebJson, value);
    }

    #endregion 灞炴€?

    #region 鏂规硶

    /// <summary>
    /// Selects file.
    /// UI 缁戝畾鐨勬柟娉?
    /// </summary>
    [UsedImplicitly]
    public void SelectFile()
    {
        var dialog = new OpenFileDialog {
            Filter = "JSON|*.json",
        };

        if (dialog.ShowDialog() == true)
        {
            Filename = dialog.FileName;
        }
    }

    /// <summary>
    /// Paste clipboard contents.
    /// UI 缁戝畾鐨勬柟娉?
    /// </summary>
    [UsedImplicitly]
    public void PasteClipboard()
    {
        if (Clipboard.ContainsText())
        {
            Filename = Clipboard.GetText().Trim();
        }
        else if (Clipboard.ContainsFileDropList())
        {
            DropFile(Clipboard.GetFileDropList()[0]);
        }
    }


    /// <summary>
    /// 鎵归噺瀵煎叆浣滀笟
    /// UI 缁戝畾鐨勬柟娉?
    /// </summary>
    /// <returns>Task</returns>
    [UsedImplicitly]
    public async Task ImportFiles()
    {
        var dialog = new OpenFileDialog {
            Filter = "JSON|*.json",
            Multiselect = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        CopilotId = 0;
        _copilotCache = null;
        foreach (var file in dialog.FileNames)
        {
            var fileInfo = new FileInfo(file);
            if (!fileInfo.Exists)
            {
                AddLog(LocalizationHelper.GetString("CopilotNoFound") + file, showTime: false);
                return;
            }

            try
            {
                using var reader = new StreamReader(File.OpenRead(file));
                var str = await reader.ReadToEndAsync();
                var payload = JsonConvert.DeserializeObject<CopilotBase>(str, new CopilotContentConverter());
                if (payload is CopilotModel copilot)
                {
                    var difficulty = copilot.Difficulty;
                    if (difficulty == CopilotModel.DifficultyFlags.None)
                    {
                        difficulty = CopilotModel.DifficultyFlags.Normal;
                    }

                    await AddCopilotTaskToList(copilot, difficulty);
                }
                else if (payload is SSSCopilotModel sss)
                {
                    await AddSSSCopilotTaskToList(sss);
                }
            }
            catch
            {
                AddLog(LocalizationHelper.GetString("CopilotFileReadError"), UiLogColor.Error, showTime: false);
                return;
            }
        }
    }

    // UI 缁戝畾鐨勬柟娉?
    [UsedImplicitly]
    public async Task AddCopilotTask()
    {
        await AddCopilotTaskToList(CopilotTaskName, false);
        CopilotTaskName = string.Empty;
    }

    // UI 缁戝畾鐨勬柟娉?
    [UsedImplicitly]
    public async Task AddCopilotTask_Adverse()
    {
        await AddCopilotTaskToList(CopilotTaskName, true);
        CopilotTaskName = string.Empty;
    }

    // UI 缁戝畾鐨勬柟娉?
    [UsedImplicitly]
    public void SelectCopilotTask(object? sender, MouseButtonEventArgs? e = null)
    {
        if (e?.Source is FrameworkElement element && element.Tag is int index)
        {
            Filename = CopilotItemViewModels[index].FilePath; // 鍋囪鍘熸柟娉曟帴鍙梚nt鍙傛暟
            if (e.ChangedButton == MouseButton.Right)
            {
                UseCopilotList = false;
            }
        }
    }

    // UI 缁戝畾鐨勬柟娉?
    [UsedImplicitly]
    public void DeleteCopilotTask(int index)
    {
        CopilotItemViewModels.RemoveAt(index);
        CopilotItemIndexChanged();
    }

    // UI 缁戝畾鐨勬柟娉?
    [UsedImplicitly]
    public void CleanUnableCopilotTask()
    {
        foreach (var item in CopilotItemViewModels.Where(model => !model.IsChecked).ToList())
        {
            CopilotItemViewModels.Remove(item);
        }

        CopilotItemIndexChanged();
    }

    // UI 缁戝畾鐨勬柟娉?
    [UsedImplicitly]
    public void ClearCopilotTask()
    {
        CopilotItemViewModels.Clear();
        SaveCopilotTask();

        try
        {
            Directory.Delete(CopilotJsonDir, true);
        }
        catch
        {
            // ignored
        }
    }

    // UI 缁戝畾鐨勬柟娉?
    [UsedImplicitly]
    public async Task LikeWebJson()
    {
        CouldLikeWebJson = false;
        if (await RateCopilot(CopilotId) == PrtsStatus.Success)
        {
            AchievementTrackerHelper.Instance.AddProgressToGroup(AchievementIds.CopilotLikeGroup);
        }
    }

    // UI 缁戝畾鐨勬柟娉?
    [UsedImplicitly]
    public void DislikeWebJson()
    {
        CouldLikeWebJson = false;
        _ = RateCopilot(CopilotId, false);
    }

    #endregion 鏂规硶

    #endregion UI缁戝畾鍙婃搷浣?

    private async Task UpdateFilename(string filename)
    {
        StartEnabled = false;
        await UpdateFileDoc(filename);
        StartEnabled = true;
    }

    private async Task UpdateFileDoc(string filename)
    {
        ClearLog();
        CopilotUrl = CopilotUiUrl;
        VideoUrl = string.Empty;
        MapUrl = MapUiUrl;
        IsDataFromWeb = false;
        CopilotId = 0;
        _copilotCache = null;

        int copilotId = 0;
        bool writeToCache = false;
        object? payload;

        if (string.IsNullOrEmpty(filename))
        {
            return;
        }
        if (File.Exists(filename))
        {
            var fileSize = new FileInfo(filename).Length;
            bool isJsonFile = filename.ToLower().EndsWith(".json") || fileSize < 4 * 1024 * 1024;
            if (!isJsonFile)
            {
                AddLog(LocalizationHelper.GetString("NotCopilotJson"), UiLogColor.Error, showTime: false);
                return;
            }

            try
            {
                using var reader = new StreamReader(File.OpenRead(filename));
                var str = await reader.ReadToEndAsync();
                payload = JsonConvert.DeserializeObject<CopilotBase>(str, new CopilotContentConverter());
            }
            catch (Exception e)
            {
                AddLog(LocalizationHelper.GetString("CopilotFileReadError") + $"\n{e.Message}", UiLogColor.Error, showTime: false);
                return;
            }
        }
        else if (TryParseCopilotCode(filename, out var codeType, out var copilotSetId))
        {
            if (codeType == CopilotCodeType.CopilotSet)
            {
                await GetCopilotSetAsync(copilotSetId);
                return;
            }

            // 鍗曚釜浣滀笟
            (copilotId, payload) = await GetCopilotAsync(filename);
            if (payload is not null)
            {
                IsDataFromWeb = true;
                writeToCache = true;
                CopilotId = copilotId;
            }
        }
        else
        {
            payload = null;
        }

        switch (payload)
        {
            case CopilotModel copilot:
                await ParseCopilotAsync(copilot, writeToCache, UseCopilotList, copilotId);
                return;
            case SSSCopilotModel sss:
                await ParseSSSCopilot(sss, writeToCache);
                return;
            default:
                AddLog(LocalizationHelper.GetString("CopilotJsonError"), UiLogColor.Error, showTime: false);
                return;
        }
    }

    /// <summary>
    /// 涓鸿嚜鍔ㄦ垬鏂楀垪琛ㄥ尮閰嶅悕瀛?
    /// </summary>
    /// <param name="names">鐢ㄤ簬鍖归厤鐨勫悕瀛?/param>
    /// <returns>鍏冲崱鍚?or string.Empty</returns>
    private static string? FindStageName(params string[] names)
    {
        names = names.Where(str => !string.IsNullOrEmpty(str)).ToArray();
        if (names.Length == 0)
        {
            return string.Empty;
        }

        // 涓€鏃︽湁鐢卞皬鍐欏瓧姣嶃€佹暟瀛椼€?-'缁勬垚鐨刵ame鍒欒涓哄叧鍗″悕鐩存帴浣跨敤
        var directName = names.FirstOrDefault(name => Regex.IsMatch(name.ToLower(), @"^[0-9a-z\-]+$"));
        if (!string.IsNullOrEmpty(directName))
        {
            return directName;
        }

        var regex = new Regex(StageNameRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        return names.Select(str => regex.Match(str)).FirstOrDefault(result => result.Success)?.Value ?? string.Empty;
    }

    #region 浣滀笟瑙ｆ瀽

    private async Task<(int CopilotId, CopilotBase? Payload)> GetCopilotAsync(string copilotCodeString)
    {
        if (!TryParseCopilotCode(copilotCodeString, out _, out var copilotCode))
        {
            AddLog(LocalizationHelper.GetString("CopilotNoFound") + $":{copilotCodeString}", UiLogColor.Error, showTime: false);
            return (0, null);
        }

        return await GetCopilotAsync(copilotCode);
    }

    private async Task<(int CopilotId, CopilotBase? Payload)> GetCopilotAsync(int copilotId)
    {
        var (status, copilotset) = await RequestCopilotAsync(copilotId);
        if (status == PrtsStatus.NetworkError)
        {
            return (0, null);
        }
        else if (status == PrtsStatus.Success && copilotset is PrtsCopilotModel { StatusCode: 200 })
        {
            if (copilotset.Data?.Content is CopilotModel { } copilot)
            {
                return (copilotId, copilot); // await ParseCopilotAsync(copilot, true, copilotList, copilotId));
            }
            else if (copilotset.Data?.Content is SSSCopilotModel { } sss)
            {
                return (copilotId, sss); // await ParseSSSCopilot(sss, true));
            }

            AddLog(LocalizationHelper.GetString("CopilotJsonError"), UiLogColor.Error, showTime: false);
            return (0, null);
        }

        AddLog(LocalizationHelper.GetString("CopilotNoFound") + $":{copilotId}", UiLogColor.Error, showTime: false);
        return (0, null);
    }

    private async Task<bool> ParseCopilotAsync(CopilotModel copilot, bool writeToCache, bool copilotList, int copilotId, bool printInfo = true)
    {
        if (string.IsNullOrEmpty(copilot.StageName))
        {
            AddLog(LocalizationHelper.GetString("CopilotJsonError"), UiLogColor.Error, showTime: false);
            return false;
        }

        _taskType = AsstTaskType.Copilot;
        _copilotCache = copilot;
        VideoUrl = string.Empty;
        if (copilot.Documentation?.Details is not null)
        {
            var linkParser = BVRegex();
            var match = linkParser.Match(copilot.Documentation.Details);
            if (match.Success)
            {
                VideoUrl = MaaUrls.BilibiliVideo + match.Value; // 瑙嗛閾炬帴
            }
        }

        bool is_corrected = false;
        var list = copilot.Opers.Concat(copilot.Groups.SelectMany(g => g.Opers)).ToList();
        foreach (var oper in list)
        {
            var character = DataHelper.GetCharacterByNameOrAlias(oper.Name);
            int rarity = character?.Rarity ?? -1;
            string id = character?.Id ?? string.Empty;
            switch (oper.Skill)
            {
                case 3 when rarity < 6 && id != "char_002_amiya":
                case 2 when rarity < 4:
                case 1 when rarity < 3:
                    AddLog(LocalizationHelper.GetStringFormat("Copilot.UnsupportedSkill", DataHelper.GetLocalizedCharacterName(oper.Name) ?? oper.Name, oper.Skill), UiLogColor.Warning, showTime: false);
                    is_corrected = true;
                    oper.Skill = 0;
                    break;
            }
            int skillElite = oper.Skill - 1;
            int skilLevelElite = oper.Requirements?.SkillLevel switch {
                <= 4 => 0,
                <= 7 => 1,
                <= 10 => 2,
                _ => 0,
            };
            int moduleElite = oper.Requirements?.Module > 0 ? 2 : 0;
            int eliteReq = Math.Max(skillElite, Math.Max(skilLevelElite, moduleElite));
            if (eliteReq > 0)
            {
                if (oper.Requirements is null)
                {
                    oper.Requirements ??= new();
                    oper.Requirements.Elite = eliteReq;
                    AddLog(LocalizationHelper.GetStringFormat("Copilot.EliteEmpty", DataHelper.GetLocalizedCharacterName(oper.Name) ?? oper.Name, eliteReq), UiLogColor.Info, showTime: false);
                }
                else if (oper.Requirements.Elite < eliteReq)
                {
                    AddLog(LocalizationHelper.GetStringFormat("Copilot.EliteAjust", DataHelper.GetLocalizedCharacterName(oper.Name) ?? oper.Name, oper.Requirements.Elite, eliteReq), UiLogColor.Warning, showTime: false);
                    oper.Requirements.Elite = eliteReq;
                    is_corrected = true;
                }
            }
        }
        if (printInfo)
        {
            foreach (var (output, color) in copilot.Output())
            {
                AddLog(output, color ?? UiLogColor.Message, showTime: false); // 浣滀笟淇℃伅杈撳嚭
            }
        }

        MapUrl = MapUiUrl.Replace("areas", "map/" + copilot.StageName);
        var mapInfo = DataHelper.FindMap(copilot.StageName);
        var navigateName = mapInfo?.Code;
        if (navigateName is null)
        {
            // 涓嶆敮鎸佺殑鍏冲崱
            AddLog(LocalizationHelper.GetStringFormat("UnsupportedStages", copilot.StageName), UiLogColor.Error, showTime: false);
            navigateName = FindStageName(copilot.Documentation?.Title ?? string.Empty);
            _ = Task.Run(ResourceUpdater.ResourceUpdateAndReloadAsync);
            AchievementTrackerHelper.Instance.Unlock(AchievementIds.MapOutdated);
            return true;
        }

        if (mapInfo?.StageId is { } stageId)
        {
            if (GetCopilotType(stageId) is CopilotType type and not CopilotType.Unknown)
            {
                CopilotTabIndex = (int)type;
                if (copilotList)
                {
                    UseCopilotList = CopilotTabIndex is 0 or 2;
                }
            }
        }

        if (!writeToCache)
        {// 鐜板湪鏄殏鏃跺皢鎵€鏈夋湰鍦颁綔涓氫笉娣诲姞鍒板垪琛?
        }
        else if (copilotList)
        {
            switch (copilot.Difficulty)
            {
                case CopilotModel.DifficultyFlags.None:
                    await AddCopilotTaskToList(copilot, CopilotModel.DifficultyFlags.Normal, copilotId: is_corrected ? default : copilotId);
                    break;
                default:
                    await AddCopilotTaskToList(copilot, copilot.Difficulty, copilotId: is_corrected ? default : copilotId);
                    break;
            }
        }
        else
        {
            try
            {
                var json = JsonConvert.SerializeObject(copilot, Formatting.Indented, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, });
                await File.WriteAllTextAsync(TempCopilotFile, json);
            }
            catch
            {
                _logger.Error("Could not save copilot task to file: " + TempCopilotFile);
                return false;
            }
        }

        return true;
    }

    private async Task<bool> ParseSSSCopilot(SSSCopilotModel copilot, bool writeToCache)
    {
        if (string.IsNullOrEmpty(copilot.StageName) || copilot.Type != new SSSCopilotModel().Type)
        {
            return false;
        }

        _taskType = AsstTaskType.SSSCopilot;
        CopilotTabIndex = 1;
        _copilotCache = copilot;
        MapUrl = MapUiUrl.Replace("areas", "map/" + copilot.StageName);
        VideoUrl = string.Empty;
        if (copilot.Documentation?.Details is not null)
        {
            var linkParser = new Regex(@"(?:av\d+|bv[a-z0-9]{10})(?:\/\?p=\d+)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var match = linkParser.Match(copilot.Documentation.Details);
            if (match.Success)
            {
                VideoUrl = MaaUrls.BilibiliVideo + match.Value; // 瑙嗛閾炬帴
            }
        }

        foreach (var (output, color) in copilot.Output())
        {
            AddLog(output, color ?? UiLogColor.Message, showTime: false);
        }

        // 涓嶆敮鎸佺殑鍏冲崱
        var stages = copilot.Stages?.Select(copilot => DataHelper.FindMap(copilot.StageName));
        if (stages?.Any(i => i is null) is null or true)
        {
            AddLog(LocalizationHelper.GetStringFormat("UnsupportedStages", copilot.StageName), UiLogColor.Error, showTime: false);
            _ = Task.Run(ResourceUpdater.ResourceUpdateAndReloadAsync);
            AchievementTrackerHelper.Instance.Unlock(AchievementIds.MapOutdated);
        }

        if (writeToCache)
        {
            try
            {
                await File.WriteAllTextAsync(TempCopilotFile, JsonConvert.SerializeObject(copilot, Formatting.Indented, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, }));
            }
            catch
            {
                _logger.Error("Could not save copilot task to file: " + TempCopilotFile);
                return false;
            }
        }

        // await AddSSSCopilotTaskToList(copilot, CopilotId); 淇濆叏浣滀笟娴忚鏃朵笉鑷姩娣诲姞鍒板垪琛?
        return true;
    }

    #endregion 浣滀笟瑙ｆ瀽

    #region 浣滀笟闆嗚В鏋?

    private async Task GetCopilotSetAsync(string copilotCodeString)
    {
        if (!TryParseCopilotCode(copilotCodeString, out _, out var copilotCode))
        {
            AddLog(LocalizationHelper.GetString("CopilotNoFound") + $"  {copilotCodeString}", UiLogColor.Error, showTime: false);
            return;
        }

        await GetCopilotSetAsync(copilotCode);
    }

    private async Task GetCopilotSetAsync(int copilotCode)
    {
        var (status, copilotset) = await RequestCopilotSetAsync(copilotCode);
        if (status == PrtsStatus.NetworkError)
        {
            return;
        }
        else if (status == PrtsStatus.Success && copilotset is PrtsCopilotSetModel { StatusCode: 200 })
        {
            await ParseCopilotSetAsync(copilotset.Data);
            return;
        }

        AddLog(LocalizationHelper.GetString("CopilotNoFound") + $"  {copilotCode}", UiLogColor.Error, showTime: false);
        return;
    }

    private async Task ParseCopilotSetAsync(PrtsCopilotSetModel.CopilotSetData? copilotSet)
    {
        CopilotId = 0;
        _copilotCache = null;
        ClearLog();
        if (copilotSet?.CopilotIds is null)
        {
            AddLog(LocalizationHelper.GetString("CopilotJsonError"), UiLogColor.Error, showTime: false);
            return;
        }
        else if (copilotSet.CopilotIds.Count == 0)
        {
            Log(copilotSet.Name, copilotSet.Description);
            return;
        }

        var list = copilotSet.CopilotIds.Select(async (copilotId) => await GetCopilotAsync(copilotId)).ToList();
        foreach (var task in list)
        {
            var (copilotId, payload) = await task;
            if (payload is CopilotModel copilot)
            {
                if (!await ParseCopilotAsync(copilot, true, true, copilotId, false))
                {
                    AddLog(LocalizationHelper.GetString("CopilotJsonError") + $", copilotId: {copilotId}", UiLogColor.Error, showTime: false);
                    continue;
                }
                var opers = JArray.FromObject(copilot.Opers.Select(i => i.Name));
                opers = JArray.FromObject(opers.Union(JArray.FromObject(copilot.Groups.Select(i => i.Opers.Select(op => op.Name)))));
                AddLog(opers.ToString(Formatting.None), UiLogColor.Message, showTime: false);
            }
            else if (payload is SSSCopilotModel sss)
            {
                CopilotTabIndex = 1;
                await AddSSSCopilotTaskToList(sss, copilotId);
            }
        }

        Log(copilotSet.Name, copilotSet.Description);
        _copilotCache = null;
        return;

        void Log(string? name, string? description)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                AddLog(name, UiLogColor.Message, showTime: false);
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                AddLog(description, UiLogColor.Message, showTime: false);
            }
        }
    }

    #endregion 浣滀笟闆嗚В鏋?

    /// <summary>
    /// Drops file.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event arguments.</param>
    /// TODO: 涓嶇煡閬撲负鍟ョ幇鍦ㄦ嫋鏀句笉鐢ㄤ簡锛屼箣鍚庣瀰鐬?
    [UsedImplicitly]
    public void DropFile(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var filename = ((Array?)e.Data.GetData(DataFormats.FileDrop))?.GetValue(0)?.ToString();
        DropFile(filename);
    }

    private void DropFile(string? filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            return;
        }

        var filenameLower = filename.ToLower();
        bool support = _supportExt.Any(ext => filenameLower.EndsWith(ext));

        if (support)
        {
            Filename = filename;
        }
        else
        {
            Filename = string.Empty;
            ClearLog();
            AddLog(LocalizationHelper.GetString("NotCopilotJson"), UiLogColor.Error, showTime: false);
        }
    }

    /// <summary>
    /// Load file items for TreeView.
    /// </summary>
    [UsedImplicitly]
    public void LoadFileItems()
    {
        try
        {
            _copilotJsonPathMap.Clear();
            FileItems.Clear();

            var copilotRoot = Path.Combine(ResourceDir, "copilot");

            // 鑾峰彇鏍圭洰褰曚笅鐨勬墍鏈夌洰褰曞拰鏂囦欢
            var directories = Directory.GetDirectories(copilotRoot);
            var rootFiles = Directory.GetFiles(copilotRoot, "*.json");

            // 娣诲姞鏍圭洰褰曚笅鐨勬枃浠?
            foreach (var file in rootFiles)
            {
                var fileName = Path.GetFileName(file);
                var relativePath = Path.GetRelativePath(copilotRoot, file);
                _copilotJsonPathMap[fileName] = file;

                FileItems.Add(new CopilotFileItem {
                    Name = fileName,
                    FullPath = file,
                    RelativePath = relativePath,
                    IsFolder = false,
                });
            }

            // 娣诲姞鏍圭洰褰曚笅鐨勬枃浠跺す锛堟敮鎸佸祵濂楋紝old 鏂囦欢澶规斁鍦ㄦ渶鍚庯級
            var oldFolderItem = (CopilotFileItem?)null;
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                var folderItem = LoadFolderItem(dir, copilotRoot);
                if (folderItem != null)
                {
                    if (dirName.Equals("old", StringComparison.OrdinalIgnoreCase))
                    {
                        oldFolderItem = folderItem;
                    }
                    else
                    {
                        FileItems.Add(folderItem);
                    }
                }
            }

            // 灏?old 鏂囦欢澶规坊鍔犲埌鏈€鍚?
            if (oldFolderItem != null)
            {
                FileItems.Add(oldFolderItem);
            }
        }
        catch (Exception exception)
        {
            FileItems.Clear();
            AddLog(exception.Message, UiLogColor.Error, showTime: false);
        }
    }

    /// <summary>
    /// 閫掑綊鍔犺浇鏂囦欢澶归」锛堟敮鎸佸祵濂楀瓙鏂囦欢澶癸級
    /// </summary>
    /// <param name="dirPath">鏂囦欢澶硅矾寰?/param>
    /// <param name="copilotRoot">copilot 鏍圭洰褰曡矾寰?/param>
    /// <returns>鏂囦欢椤癸紝濡傛灉鏂囦欢澶逛负绌哄垯杩斿洖 null</returns>
    private CopilotFileItem? LoadFolderItem(string dirPath, string copilotRoot)
    {
        var dirName = Path.GetFileName(dirPath);
        var folderItem = new CopilotFileItem {
            Name = dirName,
            IsFolder = true,
        };

        // 鑾峰彇鏂囦欢澶逛笅鐨勬墍鏈夋枃浠?
        var folderFiles = Directory.GetFiles(dirPath, "*.json");
        foreach (var file in folderFiles)
        {
            var fileName = Path.GetFileName(file);
            var relativePath = Path.GetRelativePath(copilotRoot, file);
            _copilotJsonPathMap[fileName] = file;

            folderItem.Children.Add(new CopilotFileItem {
                Name = fileName,
                FullPath = file,
                RelativePath = relativePath,
                IsFolder = false,
            });
        }

        // 鑾峰彇鏂囦欢澶逛笅鐨勬墍鏈夊瓙鏂囦欢澶癸紙閫掑綊鍔犺浇锛?
        var subDirectories = Directory.GetDirectories(dirPath);
        foreach (var subDir in subDirectories)
        {
            var subFolderItem = LoadFolderItem(subDir, copilotRoot);
            if (subFolderItem != null)
            {
                folderItem.Children.Add(subFolderItem);
            }
        }

        // 濡傛灉鏂囦欢澶逛负绌猴紙鏃㈡病鏈夋枃浠朵篃娌℃湁瀛愭枃浠跺す锛夛紝杩斿洖 null
        if (folderItem.Children.Count == 0)
        {
            return null;
        }

        return folderItem;
    }

    /// <summary>
    /// Handle file selection from TreeView.
    /// </summary>
    /// <param name="fileItem">The selected file item.</param>
    [UsedImplicitly]
    public void OnFileSelected(CopilotFileItem? fileItem)
    {
        if (fileItem == null || fileItem.IsFolder || string.IsNullOrEmpty(fileItem.FullPath))
        {
            return;
        }

        Filename = fileItem.FullPath;
    }

    private async Task AddCopilotTaskToList(string? stageName, bool isRaid)
    {
        if (!string.IsNullOrEmpty(stageName) && InvalidStageNameRegex().IsMatch(stageName))
        {
            AddLog(LocalizationHelper.GetString("CopilotInvalidStageNameForNavigation"), UiLogColor.Error, showTime: false);
            return;
        }

        try
        {
            if (_copilotCache is null)
            {
            }
            else if (_copilotCache is CopilotModel { } copilot)
            {
                await AddCopilotTaskToList(copilot, !isRaid ? CopilotModel.DifficultyFlags.Normal : CopilotModel.DifficultyFlags.Raid, stageName, CopilotId);
            }
            else if (_copilotCache is SSSCopilotModel { } sss)
            {
                await AddSSSCopilotTaskToList(sss, CopilotId);
            }
        }
        catch (Exception ex)
        {
            AddLog(LocalizationHelper.GetString("CopilotJsonError"), UiLogColor.Error, showTime: false);
            _logger.Error(ex, "Exception caught");
        }
    }

    /// <summary>
    /// 灏嗕綔涓氭坊鍔犲埌鍒楄〃
    /// </summary>
    /// <param name="copilot">浣滀笟</param>
    /// <param name="flags">闅惧害绛夌骇</param>
    /// <param name="navName">鍏冲崱 code锛岀敤浜庡鑸?/param>
    /// <param name="copilotId">浣滀笟绔?id</param>
    /// <returns>鏄惁娣诲姞浜嗕綔涓?/returns>
    private async Task<bool> AddCopilotTaskToList(CopilotModel copilot, CopilotModel.DifficultyFlags flags, string? navName = null, int copilotId = 0)
    {
        if (string.IsNullOrEmpty(copilot.StageName))
        {
            _logger.Error("Could not add copilot task with empty stage");
            return false;
        }

        if (!Path.Exists(CopilotJsonDir))
        {
            try
            {
                Directory.CreateDirectory(CopilotJsonDir);
            }
            catch
            {
                return false;
            }
        }

        var mapInfo = DataHelper.FindMap(copilot.StageName);
        var stageCode = mapInfo?.Code;
        var stageId = mapInfo?.StageId;
        if (mapInfo is null)
        {
            AddLog(LocalizationHelper.GetStringFormat("CopilotStageNameNotFound", $"{copilot.StageName}({navName})"), UiLogColor.Error, showTime: false);
            return false;
        }

        var navigateName = string.IsNullOrEmpty(navName) ? stageCode : navName;
        if (stageCode != navigateName)
        {
            stageCode = navigateName;
            AddLog(LocalizationHelper.GetString("CopilotStageNameNotEqualWithNavigateName"), UiLogColor.Warning, showTime: false);
        }

        if (stageId is null || stageCode is null || string.IsNullOrEmpty(navigateName))
        {
            return false;
        }

        var fileName = !string.IsNullOrEmpty(stageCode) ? stageCode : DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        var cachePath = Path.GetRelativePath(BaseDir, $"{CopilotJsonDir}/{fileName}.json");
        await _semaphore.WaitAsync();
        if (File.Exists(cachePath) && CopilotItemViewModels.Any(i => i.FilePath == cachePath))
        {
            cachePath = Path.GetRelativePath(BaseDir, $"{CopilotJsonDir}/{fileName}_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.json");
            if (CopilotItemViewModels.Any(i => i.FilePath == cachePath))
            {
                _logger.Error("Could not add copilot task with duplicate stage name: {StageName}", copilot.StageName);
                _semaphore.Release();
                return false;
            }
        }

        try
        {
            await File.WriteAllTextAsync(cachePath, JsonConvert.SerializeObject(copilot, Formatting.Indented, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, }));
        }
        catch
        {
            AddLog(LocalizationHelper.GetString("CopilotCouldNotSaveFile") + cachePath, UiLogColor.Error, showTime: false);
            _semaphore.Release();
            return false;
        }

        if (CopilotTabIndex == 2)
        {
            string? name = null;
            if (stageId?.Length > 6)
            {
                var codeName = stageId[4..^2];
                var characterInfo = DataHelper.GetCharacterByCodeName(codeName);
                name = DataHelper.GetLocalizedCharacterName(characterInfo);
            }

            name ??= stageCode;

            var item = new CopilotItemViewModel(name, cachePath, false, copilotId) { Index = CopilotItemViewModels.Count, };
            CopilotItemViewModels.Add(item);
        }
        else
        {
            if (flags.HasFlag(CopilotModel.DifficultyFlags.Normal))
            {
                var item = new CopilotItemViewModel(stageCode, cachePath, false, copilotId, isNavNameOverride: !string.IsNullOrEmpty(navName)) { Index = CopilotItemViewModels.Count, };
                CopilotItemViewModels.Add(item);
            }

            if (flags.HasFlag(CopilotModel.DifficultyFlags.Raid))
            {
                var item = new CopilotItemViewModel(stageCode, cachePath, true, copilotId, isNavNameOverride: !string.IsNullOrEmpty(navName)) { Index = CopilotItemViewModels.Count, };
                CopilotItemViewModels.Add(item);
            }
        }

        _semaphore.Release();
        SaveCopilotTask();
        return true;
    }

    private async Task<bool> AddSSSCopilotTaskToList(SSSCopilotModel copilot, int copilotId = 0)
    {
        if (string.IsNullOrEmpty(copilot.StageName) || copilot.Type != new SSSCopilotModel().Type)
        {
            _logger.Error("Could not add SSS copilot task with empty stage");
            return false;
        }

        if (!Path.Exists(CopilotJsonDir))
        {
            try
            {
                Directory.CreateDirectory(CopilotJsonDir);
            }
            catch
            {
                return false;
            }
        }

        var fileName = string.Concat(copilot.StageName.Split(Path.GetInvalidFileNameChars())).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        }

        var cachePath = Path.GetRelativePath(BaseDir, $"{CopilotJsonDir}/{fileName}.json");
        await _semaphore.WaitAsync();
        if (File.Exists(cachePath) && CopilotItemViewModels.Any(i => i.FilePath == cachePath))
        {
            cachePath = Path.GetRelativePath(BaseDir, $"{CopilotJsonDir}/{fileName}_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.json");
            if (CopilotItemViewModels.Any(i => i.FilePath == cachePath))
            {
                _logger.Error("Could not add SSS copilot task with duplicate stage name: {StageName}", copilot.StageName);
                _semaphore.Release();
                return false;
            }
        }

        try
        {
            await File.WriteAllTextAsync(cachePath, JsonConvert.SerializeObject(copilot, Formatting.Indented, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, }));
        }
        catch
        {
            AddLog(LocalizationHelper.GetString("CopilotCouldNotSaveFile") + cachePath, UiLogColor.Error, showTime: false);
            _semaphore.Release();
            return false;
        }

        var item = new CopilotItemViewModel(copilot.StageName, cachePath, false, copilotId) { Index = CopilotItemViewModels.Count, };
        CopilotItemViewModels.Add(item);

        _semaphore.Release();
        SaveCopilotTask();
        return true;
    }

    public void SaveCopilotTask()
    {
        ConfigFactory.CurrentConfig.Copilot.TaskList = [.. CopilotItemViewModels];
    }

    /// <summary>
    /// 鎴樻枟鍒楄〃鐨勫綋鍓嶆垬鏂椾换鍔℃垚鍔?
    /// </summary>
    public void CopilotTaskSuccess()
    {
        Execute.OnUIThread(() => {
            foreach (var model in CopilotItemViewModels)
            {
                if (!model.IsChecked || (CurrentCopilotId != -1 && model.Index != CurrentCopilotId))
                {
                    continue;
                }

                model.IsChecked = false;

                if (model.CopilotId > 0 && _copilotIdList.Remove(model.CopilotId) && _copilotIdList.IndexOf(model.CopilotId) == -1 && !HasRequirementIgnored)
                {
                    _ = RateCopilot(model.CopilotId);
                }

                break;
            }

            SaveCopilotTask();
        });
    }

    /// <summary>
    /// 鏇存柊浠诲姟椤哄簭
    /// </summary>
    public void CopilotItemIndexChanged()
    {
        for (int i = 0; i < CopilotItemViewModels.Count; i++)
        {
            CopilotItemViewModels[i].Index = i;
        }

        SaveCopilotTask();
    }

    /// <summary>
    /// Starts copilot.
    /// UI 缁戝畾鐨勬柟娉?
    /// </summary>
    /// <returns>Task</returns>
    [UsedImplicitly]
    public async Task Start()
    {
        /*
        if (_form)
        {
            AddLog(Localization.GetString("AutoSquadTip"), LogColor.Message);
        }*/
        _runningState.SetIdle(false);

        Instances.OverlayViewModel.LogItemsSource = LogItemViewModels;

        // if (_taskType == AsstTaskType.VideoRecognition)
        // {
        //     _ = StartVideoTask();
        //     return;
        // }

        // 缁熶竴鍓嶇疆鏍￠獙锛氬厛鎸?CopilotTabIndex 鍒嗗彂锛屽啀鍒ゆ柇瀵瑰簲閫夐」锛圲seCopilotList 绛夛級
        if (!await ValidateStartAsync())
        {
            _runningState.SetIdle(true);
            return;
        }

        await RunStartsWithScriptAsync();

        if (!await ConnectToEmulatorAsync())
        {
            await Stop();
            return;
        }

        // 杩炴帴鏈熼棿鐢ㄦ埛鍙兘宸茬偣鍋滄锛岄渶鍦ㄦ澶勬嫤鎴?
        if (_runningState.GetStopping())
        {
            Instances.TaskQueueViewModel.SetStopped(SettingsViewModel.GameSettings.CopilotWithScript);
            AddLog(LocalizationHelper.GetString("Stopped"));
            return;
        }

        var userAdditional = ParseUserAdditionals();

        bool ret;
        try
        {
            ret = await AppendAndStartCopilotAsync(userAdditional);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start copilot task");
            AddLog(LocalizationHelper.GetString("CopilotStartError") + ex.Message, UiLogColor.Error, showTime: false);
            ret = false;
        }

        if (ret)
        {
            AddLog(LocalizationHelper.GetString("Running"));
        }
        else
        {
            if (!Instances.AsstProxy.AsstStop())
            {
                _logger.Warning("Failed to stop Asst");
            }

            _runningState.SetIdle(true);
            AddLog(LocalizationHelper.GetString("CopilotFileReadError"), UiLogColor.Error, showTime: false);
        }
    }

    private async Task<bool> ValidateStartAsync()
    {
        if (UseCopilotList)
        {
            // 鍒楄〃妯″紡锛氬彧鏍￠獙鍒楄〃鏈韩锛屼笉妫€鏌ヨ緭鍏ユ閲岀殑鍗曟枃浠朵綔涓氱被鍨?
            return await ValidateTaskListStrictAsync(tabIndex: CopilotTabIndex);
        }

        // 闈炲垪琛ㄦā寮忓繀椤绘湁褰撳墠浣滀笟
        if (_copilotCache is null)
        {
            AddLog(LocalizationHelper.GetString("CopilotEmptyError"), UiLogColor.Error, showTime: false);
            return false;
        }

        // 闈炲垪琛ㄦā寮忥細妫€鏌ュ崟鏂囦欢浣滀笟鐨?_taskType 涓?CopilotTabIndex 鏄惁鍖归厤
        if ((_taskType == AsstTaskType.SSSCopilot && CopilotTabIndex != 1) || (_taskType != AsstTaskType.SSSCopilot && CopilotTabIndex == 1))
        {
            AddLog(LocalizationHelper.GetString("CopilotTaskTypeMismatch"), UiLogColor.Error, showTime: false);
            return false;
        }

        return true;
    }

    private async Task<bool> ValidateTaskListStrictAsync(int tabIndex)
    {
        var selected = CopilotItemViewModels.Where(i => i.IsChecked).ToArray();

        // 绌哄垪琛細鎻愮ず骞跺け璐?
        if (selected.Length == 0)
        {
            AddLog(LocalizationHelper.GetString("CopilotStartWithEmptyList"), UiLogColor.Error, showTime: false);
            return false;
        }

        if (tabIndex != 3)
        {
            var types = new HashSet<CopilotType>(await Task.WhenAll(selected.Select(item => GetCopilotTypeAsync(item.FilePath))));
            var concreteTypes = types.Where(type => type != CopilotType.Unknown).ToArray();
            if (types.Contains(CopilotType.Unknown))
            {
                AddLog(LocalizationHelper.GetString("CopilotTaskTypeParseFailedSkipCheck"), UiLogColor.Error, showTime: false);
            }

            if (concreteTypes.Length > 1)
            {
                AddLog(LocalizationHelper.GetString("CopilotTaskListMixedModeNotAllowed") + "\n" + string.Join(", ", concreteTypes.Select(GetCopilotTabName)), UiLogColor.Error, showTime: false);
                return false;
            }
        }

        // 鍏堝垽鏂?CopilotTabIndex锛屽啀鍒ゆ柇瀵瑰簲閫夐」
        if (tabIndex == 2)
        {
            return VerifyParadoxTasks(selected);
        }

        return await VerifyCopilotListTask(selected);
    }

    private async Task RunStartsWithScriptAsync()
    {
        if (!SettingsViewModel.GameSettings.CopilotWithScript)
        {
            return;
        }

        await Task.Run(() => SettingsViewModel.GameSettings.RunScript("StartsWithScript", showLog: false));
        if (!string.IsNullOrWhiteSpace(SettingsViewModel.GameSettings.StartsWithScript))
        {
            AddLog(LocalizationHelper.GetString("StartsWithScript"));
        }
    }

    private async Task<bool> ConnectToEmulatorAsync()
    {
        AddLog(LocalizationHelper.GetString("ConnectingToEmulator"));

        string errMsg = string.Empty;
        bool caught = await Task.Run(() => Instances.AsstProxy.AsstConnect(ref errMsg));
        if (caught)
        {
            return true;
        }

        AddLog(errMsg, UiLogColor.Error);
        return false;
    }

    private IEnumerable<UserAdditional> ParseUserAdditionals()
    {
        foreach (var op in UserAdditional)
        {
            if (string.IsNullOrWhiteSpace(op.Name))
            {
                continue;
            }

            op.Skill = Math.Clamp(op.Skill, 0, 3);
        }

        return UserAdditional.Where(op => !string.IsNullOrWhiteSpace(op.Name));
    }

    private async Task<bool> AppendAndStartCopilotAsync(IEnumerable<UserAdditional> userAdditional)
    {
        if (!UseCopilotList)
        {
        }
        else if (CopilotTabIndex == 0)
        {
            _copilotIdList.Clear();

            var t = CopilotItemViewModels.Where(i => i.IsChecked).Select(i => {
                _copilotIdList.Add(i.CopilotId);
                return new MultiTask { Index = i.Index, FileName = i.FilePath, IsRaid = i.IsRaid, StageName = i.IsNavNameOverride ? i.Name : null, };
            });

            var task = new AsstCopilotTask() {
                MultiTasks = [.. t],
                Formation = Form,
                SupportUnitUsage = UseSupportUnitUsage ? (int)SupportUnitUsage : 0,
                AddTrust = AddTrust,
                IgnoreRequirements = IgnoreRequirements,
                UserAdditionals = AddUserAdditional ? [.. userAdditional] : [],
                UseSanityPotion = UseSanityPotion,
                FormationIndex = UseFormation ? FormationIndex : 0,
            };

            // 鑳界敤鍒楄〃鐨勬槸涓荤嚎/ss/鏁呬簨闆?鎮栬锛岄兘鏄?Copilot 绫诲瀷
            var ret = Instances.AsstProxy.AsstAppendTaskWithEncoding(AsstProxy.TaskType.Copilot, task).IsSuccess;
            return ret && Instances.AsstProxy.AsstStart();
        }
        else if (CopilotTabIndex == 2)
        {
            _copilotIdList.Clear();

            var t = CopilotItemViewModels.Where(i => i.IsChecked).Select(i => {
                _copilotIdList.Add(i.CopilotId);
                return new AsstParadoxCopilotTask.MultiTask(i.Index, i.FilePath);
            });

            var task = new AsstParadoxCopilotTask() { MultiTasks = [.. t], };
            var ret = Instances.AsstProxy.AsstAppendTaskWithEncoding(AsstProxy.TaskType.Copilot, task).IsSuccess;
            return ret && Instances.AsstProxy.AsstStart();
        }
        else
        {
            return false;
        }

        if (IsDataFromWeb)
        {
            try
            {
                await File.WriteAllTextAsync(TempCopilotFile, JsonConvert.SerializeObject(_copilotCache, Formatting.Indented, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, }));
            }
            catch
            {
                AddLog(LocalizationHelper.GetString("CopilotCouldNotSaveFile") + TempCopilotFile, UiLogColor.Error);
                return false;
            }
        }

        bool appended;
        if (CopilotTabIndex == 2)
        {
            var singleTask = new AsstParadoxCopilotTask() { FileName = IsDataFromWeb ? TempCopilotFile : Filename };
            appended = Instances.AsstProxy.AsstAppendTaskWithEncoding(AsstProxy.TaskType.Copilot, singleTask).IsSuccess;
        }
        else
        {
            var singleTask = new AsstCopilotTask() {
                FileName = IsDataFromWeb ? TempCopilotFile : Filename,
                Formation = Form,
                SupportUnitUsage = UseSupportUnitUsage ? (int)SupportUnitUsage : 0,
                AddTrust = AddTrust,
                IgnoreRequirements = IgnoreRequirements,
                UserAdditionals = AddUserAdditional ? [.. userAdditional] : [],
                LoopTimes = Loop ? LoopTimes : 1,
                UseSanityPotion = false,
                FormationIndex = UseFormation ? FormationIndex : 0,
            };

            // 鍗曚綔涓氶渶瑕佸尯鍒?Copilot / SSSCopilot
            appended = Instances.AsstProxy.AsstAppendTaskWithEncoding(AsstProxy.TaskType.Copilot, _taskType, singleTask.Serialize().Params);
        }

        return appended && Instances.AsstProxy.AsstStart();
    }

    // private bool StartVideoTask()
    // {
    //     return Instances.AsstProxy.AsstStartVideoRec(Filename);
    // }

    /// <summary>
    /// Stops copilot.
    /// UI 缁戝畾鐨勬柟娉?
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task Stop()
    {
        // 绛夊緟 Core 瀹為檯鍋滄锛涘洖璋冩垨瓒呮椂鑷姩 SetStopped锛堣剼鏈敱 proxy 鍥炶皟鎸?CopilotWithScript 璁剧疆鍒ゆ柇锛?
        AddLog(LocalizationHelper.GetString("Stopping"));
        await Instances.TaskQueueViewModel.Stop();
        if (_runningState.GetIdle() && !_runningState.GetStopping())
        {
            AddLog(LocalizationHelper.GetString("Stopped"));
        }
    }

    private bool IsDataFromWeb { get => field; set => SetAndNotify(ref field, value); }

    private int _copilotId;

    private int CopilotId
    {
        get => _copilotId;
        set {
            SetAndNotify(ref _copilotId, value);
            CouldLikeWebJson = value > 0;
        }
    }

    private async Task<PrtsStatus> RateCopilot(int copilotId, bool isLike = true)
    {
        if (copilotId <= 0 || _recentlyRatedCopilotId.Contains(copilotId))
        {
            return PrtsStatus.NotFound;
        }

        var result = await RateWebJsonAsync(copilotId, isLike ? "Like" : "Dislike");
        switch (result)
        {
            case PrtsStatus.Success:
                _recentlyRatedCopilotId.Add(copilotId);
                AddLog(LocalizationHelper.GetString("ThanksForLikeWebJson"), UiLogColor.Info, showTime: false);
                break;
            case PrtsStatus.NetworkError:
                AddLog(LocalizationHelper.GetString("FailedToLikeWebJson"), UiLogColor.Error, showTime: false);
                break;
        }

        return result;
    }

    private async Task<bool> VerifyCopilotListTask()
    {
        return await VerifyCopilotListTask(null);
    }

    private async Task<bool> VerifyCopilotListTask(IEnumerable<CopilotItemViewModel>? items)
    {
        var copilotItemViewModels = (items ?? CopilotItemViewModels.Where(i => i.IsChecked)).ToArray();
        switch (copilotItemViewModels.Length)
        {
            case 0:
                AddLog(LocalizationHelper.GetString("CopilotStartWithEmptyList"), UiLogColor.Error, showTime: false);
                return false;
            case 1:
                AddLog(LocalizationHelper.GetString("CopilotSingleTaskWarning"), UiLogColor.Warning, showTime: false);
                break; // 闄嶇骇涓鸿鍛? 鏈夌敤鎴风偢灏辨淳uuu
        }

        if (copilotItemViewModels.Any(i => string.IsNullOrEmpty(i.Name?.Trim())))
        {
            AddLog(LocalizationHelper.GetString("CopilotTasksWithEmptyName"), UiLogColor.Error, showTime: false);
            return false;
        }

        var stageNames = copilotItemViewModels.Select(i => i.FilePath).ToHashSet().Select(async path => {
            if (!File.Exists(path))
            {
                AddLog(LocalizationHelper.GetString("CopilotNoFound") + path, UiLogColor.Error, showTime: false);
                return null;
            }

            try
            {
                var str = await File.ReadAllTextAsync(path);
                return JsonConvert.DeserializeObject<CopilotModel>(str)?.StageName;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "could not read & parse copilot file: {Path}", path);
                return null;
            }
        });
        foreach (var stageName in stageNames)
        {
            var name = await stageName;
            if (!string.IsNullOrEmpty(name) && DataHelper.FindMap(name) is not null)
            {
                continue;
            }

            AddLog(LocalizationHelper.GetStringFormat("UnsupportedStages", name), UiLogColor.Error, showTime: false);
            _ = Task.Run(ResourceUpdater.ResourceUpdateAndReloadAsync);
            AchievementTrackerHelper.Instance.Unlock(AchievementIds.MapOutdated);
            return false;
        }

        return true;
    }

    private bool VerifyParadoxTasks(IEnumerable<CopilotItemViewModel>? items = null)
    {
        var ok = true;
        foreach (var task in items ?? CopilotItemViewModels.Where(i => i.IsChecked))
        {
            if (!DataHelper.Operators.Any(op => op.Value.Name == DataHelper.GetLocalizedCharacterName(task.Name, "zh-cn")))
            {
                AddLog(LocalizationHelper.GetStringFormat("CopilotIllegalOperName", task.Name), UiLogColor.Error, showTime: false);
                _ = Task.Run(ResourceUpdater.ResourceUpdateAndReloadAsync);
                AchievementTrackerHelper.Instance.Unlock(AchievementIds.MapOutdated);
                ok = false;
            }
        }

        return ok;
    }

    private static string GetCopilotTabName(CopilotType type) =>
         type switch {
             CopilotType.MainStageAndSideStory => LocalizationHelper.GetString("MainStageStoryCollectionSideStory"),
             CopilotType.SSS => LocalizationHelper.GetString("SSS"),
             CopilotType.Paradox => LocalizationHelper.GetString("ParadoxSimulation"),
             CopilotType.Other => LocalizationHelper.GetString("OtherActivityStage"),
             _ => type.ToString(),
         };

    /// <summary>
    /// 鐐瑰嚮鍚庣Щ闄ょ晫闈腑鍏冪礌鐒︾偣
    /// </summary>
    /// <param name="sender">鐐瑰嚮浜嬩欢鍙戦€佽€?/param>
    /// <param name="e">鐐瑰嚮浜嬩欢</param>
    [UsedImplicitly]
    public void MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        DependencyObject scope = FocusManager.GetFocusScope(element);
        FocusManager.SetFocusedElement(scope, element);
        Keyboard.ClearFocus();
    }

    /// <summary>
    /// 鍥炶溅閿偣鍑诲悗绉婚櫎鐣岄潰涓厓绱犵劍鐐?
    /// </summary>
    /// <param name="sender">鐐瑰嚮浜嬩欢鍙戦€佽€?/param>
    /// <param name="e">鐐瑰嚮浜嬩欢</param>
    [UsedImplicitly]
    public void KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (sender is not UIElement element)
        {
            return;
        }

        DependencyObject scope = FocusManager.GetFocusScope(element);
        FocusManager.SetFocusedElement(scope, element);
        Keyboard.ClearFocus();
    }

    private static async Task<CopilotType> GetCopilotTypeAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.Error("file not found: {Path}, return: {Return}", filePath, CopilotType.Unknown);
                return CopilotType.Unknown;
            }
            var str = await File.ReadAllTextAsync(filePath);
            var job = JsonConvert.DeserializeObject<CopilotBase>(str, new CopilotContentConverter());
            return GetCopilotType(job);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "could not read & parse copilot file: {Path}", filePath);
            return CopilotType.Unknown;
        }
    }

    private static CopilotType GetCopilotType(CopilotBase? @base)
    {
        if (@base is null)
        {
            return CopilotType.Unknown;
        }
        else if (@base is SSSCopilotModel)
        {
            return CopilotType.SSS;
        }
        else if (@base is CopilotModel copilot)
        {
            if (string.IsNullOrEmpty(copilot.StageName))
            {
                return CopilotType.Unknown;
            }
            var mapInfo = DataHelper.FindMap(copilot.StageName);
            if (mapInfo is null)
            {
                return CopilotType.Unknown;
            }
            return GetCopilotType(mapInfo.StageId);
        }

        return CopilotType.Unknown;
    }

    private static CopilotType GetCopilotType(string? stageId)
    {
        if (stageId?.StartsWith("mem_") is true)
        {
            return CopilotType.Paradox;
        }
        else if (stageId?.StartsWith("lt_") is true)
        {
            return CopilotType.SSS;
        }
        else if (!string.IsNullOrEmpty(stageId))
        {
            if (MainStageIdRegex().IsMatch(stageId))
            {
                return CopilotType.MainStageAndSideStory;
            }
            if (SideStoryStageIdRegex().IsMatch(stageId))
            {
                return CopilotType.MainStageAndSideStory;
            }
        }

        return CopilotType.Unknown;
    }

    private enum CopilotType
    {
        Unknown = -1,

        /// <summary>涓荤嚎, 鏁呬簨闆? 鏀嚎浣滀笟</summary>
        MainStageAndSideStory = 0,

        /// <summary>淇濆叏</summary>
        SSS,

        /// <summary>鎮栬妯℃嫙</summary>
        Paradox,

        /// <summary>鍏朵粬</summary>
        Other,
    }

    [GeneratedRegex(@"(?:av\d+|bv[a-z0-9]{10})(?:\/\?p=\d+)?", RegexOptions.IgnoreCase | RegexOptions.Compiled, "zh-CN")]
    private static partial Regex BVRegex();
}
