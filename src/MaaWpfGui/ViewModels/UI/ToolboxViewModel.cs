// <copyright file="ToolboxViewModel.cs" company="MaaAssistantArknights">
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
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HandyControl.Controls;
using JetBrains.Annotations;
using MaaWpfGui.Configuration.Factory;
using MaaWpfGui.Configuration.Single.MaaTask;
using MaaWpfGui.Constants;
using MaaWpfGui.Constants.Enums;
using MaaWpfGui.Extensions;
using MaaWpfGui.Helper;
using MaaWpfGui.Main;
using MaaWpfGui.Models;
using MaaWpfGui.Models.AsstTasks;
using MaaWpfGui.States;
using MaaWpfGui.Utilities;
using MaaWpfGui.Utilities.ValueType;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObservableCollections;
using Serilog;
using Stylet;
using Timer = System.Timers.Timer;

namespace MaaWpfGui.ViewModels.UI;

/// <summary>
/// The view model of recruit.
/// </summary>
public partial class ToolboxViewModel : Screen
{
    private readonly RunningState _runningState;
    private static readonly ILogger _logger = Log.ForContext<ToolboxViewModel>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolboxViewModel"/> class.
    /// </summary>
    public ToolboxViewModel()
    {
        PropertyDependsOnUtility.InitializePropertyDependencies(this);
        DisplayName = LocalizationHelper.GetString("Toolbox");
        _runningState = RunningState.Instance;
        _runningState.StateChanged += (__, e) => {
            Idle = e.NewState.Idle;
            Inited = e.NewState.Inited;
            Stopping = e.NewState.Stopping;

            if (e.NewState.Idle)
            {
                PixelPaintParametersLocked = false;
            }

            if (e.NewState.Stopping && Peeping && !IsPeepTransitioning)
            {
                _ = Peep();
            }
        };
        _peepImageTimer.Elapsed += PeepImageTimerElapsed;

        // 鏈被鍨嬬敱 Stylet IoC 瀹瑰櫒绠＄悊锛屽叏搴旂敤鐢熷懡鍛ㄦ湡鍞竴瀹炰緥锛岃闃呭悗鏃犻渶鍙栨秷璁㈤槄
        LocalizationHelper.LanguageChanged += () => {
            DisplayName = LocalizationHelper.GetString("Toolbox");
            RecruitInfo = LocalizationHelper.GetString("RecruitmentRecognitionTip");
            PixelPaintFitModeList.RefreshLocalization();
            PixelPaintDitherModeList.RefreshLocalization();
            SecretFrontEventList.RefreshLocalization();
            Application.Current.Dispatcher.InvokeAsync(
                () => {
                    LoadDepotDetails();
                    ClearOperBoxRecognitionData();
                    LoadOperBoxDetails();
                    RefreshDataAccountList();
                },
                DispatcherPriority.Loaded);
        };
        SettingsViewModel.GuiSettings.OperNameLanguageChanged += () => {
            Application.Current.Dispatcher.InvokeAsync(() => {
                ClearOperBoxRecognitionData();
                LoadOperBoxDetails();
            }, DispatcherPriority.Loaded);
        };
        _peepImageTimer.Interval = 1000d / PeepTargetFps;
        _gachaTimer.Tick += RefreshGachaTip;
        InitializeAccountScopedData();
        LoadDepotDetails();
        LoadOperBoxDetails();
        InitializeDepotRowPresentation();
        InitializeOperBoxRowPresentation();
        OperBoxSelectedIndex = OperBoxNotHaveList.Count > 0 ? 0 : 1;
        RefreshDataAccountList();

        UpdateMiniGameTaskList();
    }

    private bool _idle;

    /// <summary>
    /// Gets or sets a value indicating whether it is idle.
    /// </summary>
    public bool Idle
    {
        get => _idle;
        set => SetAndNotify(ref _idle, value);
    }

    private bool _inited;

    public bool Inited
    {
        get => _inited;
        set => SetAndNotify(ref _inited, value);
    }

    private bool _stopping;

    public bool Stopping
    {
        get => _stopping;
        set => SetAndNotify(ref _stopping, value);
    }

    #region Recruit

    /// <summary>
    /// Gets or sets the recruit info.
    /// </summary>
    public string RecruitInfo { get => field; set => SetAndNotify(ref field, value); } = LocalizationHelper.GetString("RecruitmentRecognitionTip");

    public ObservableCollection<Inline> RecruitResultInlines { get => field; set => SetAndNotify(ref field, value); } = [];

    public void UpdateRecruitResult(JArray? resultArray)
    {
        ObservableCollection<Inline> recruitResultInlines = [];

        foreach (var combs in resultArray ?? [])
        {
            int tagLevel = (int)(combs["level"] ?? -1);
            var tagStr = $"{tagLevel}鈽?Tags:    ";
            tagStr = ((JArray?)combs["tags"] ?? []).Aggregate(tagStr, (current, tag) => current + $"{tag}    ");
            var tagRun = new Run(tagStr);
            tagRun.SetResourceReference(TextElement.ForegroundProperty, UiLogColor.Text);
            tagRun.Tag = UiLogColor.Text;

            recruitResultInlines.Add(tagRun);

            recruitResultInlines.Add(new LineBreak());

            var opersArray = (JArray?)combs["opers"] ?? [];

            var opersWithPotential = opersArray.Select(oper => {
                int operLevel = (int)(oper["level"] ?? -1);
                var operId = oper["id"]?.ToString();

                int pot = -1;
                if (RecruitmentShowPotential && OperBoxPotential != null && operId != null && (tagLevel >= 4 || operLevel == 1))
                {
                    if (OperBoxPotential.TryGetValue(operId, out var potentialValue))
                    {
                        pot = potentialValue;
                    }
                }

                return new { Oper = oper, Potential = pot, OperLevel = operLevel };
            })
            .OrderByDescending(x => x.OperLevel)
            .ThenBy(x => x.Potential)
            .ToList();

            foreach (var x in opersWithPotential)
            {
                var oper = x.Oper;
                int operLevel = x.OperLevel;
                var operId = oper["id"]?.ToString();
                var operName = DataHelper.GetLocalizedCharacterName(oper["name"]?.ToString());

                bool isMaxPot = false;
                string potentialText = string.Empty;

                if (RecruitmentShowPotential && OperBoxPotential != null && operId != null && (tagLevel >= 4 || operLevel == 1))
                {
                    if (OperBoxPotential.TryGetValue(operId, out var pot))
                    {
                        potentialText = $" ( {pot} )";
                        if (pot == 6)
                        {
                            isMaxPot = true;
                            potentialText = " ( MAX )";
                        }
                    }
                    else
                    {
                        potentialText = " ( !!! NEW !!! )";
                    }
                }

                var run = new Run($"{operName}{potentialText}    ");
                var brushKey = GetBrushKeyByStar(operLevel, isMaxPot);
                run.SetResourceReference(TextElement.ForegroundProperty, brushKey);
                run.Tag = brushKey;

                recruitResultInlines.Add(run);
            }

            recruitResultInlines.Add(new LineBreak());
            recruitResultInlines.Add(new LineBreak());
        }

        RecruitResultInlines = recruitResultInlines;
        return;

        string GetBrushKeyByStar(int level, bool isMax)
        {
            return (level, isMax) switch {
                (6, true) => UiLogColor.Star6OperatorPotentialFull,
                (6, false) => UiLogColor.Star6Operator,
                (5, true) => UiLogColor.Star5OperatorPotentialFull,
                (5, false) => UiLogColor.Star5Operator,
                (4, true) => UiLogColor.Star4OperatorPotentialFull,
                (4, false) => UiLogColor.Star4Operator,
                (3, true) => UiLogColor.Star3OperatorPotentialFull,
                (3, false) => UiLogColor.Star3Operator,
                (2, true) => UiLogColor.Star2OperatorPotentialFull,
                (2, false) => UiLogColor.Star2Operator,
                (1, true) => UiLogColor.Star1OperatorPotentialFull,
                (1, false) => UiLogColor.Star1Operator,
                _ => UiLogColor.Text,
            };
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to choose level 3.
    /// </summary>
    public bool ChooseLevel3
    {
        get; set {
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Toolbox.ChooseLevel3 = value;
        }
    } = ConfigFactory.CurrentConfig.Toolbox.ChooseLevel3;

    /// <summary>
    /// Gets or sets a value indicating whether to choose level 4.
    /// </summary>
    public bool ChooseLevel4
    {
        get; set {
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Toolbox.ChooseLevel4 = value;
        }
    } = ConfigFactory.CurrentConfig.Toolbox.ChooseLevel4;

    /// <summary>
    /// Gets or sets a value indicating whether to choose level 5.
    /// </summary>
    public bool ChooseLevel5
    {
        get; set {
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Toolbox.ChooseLevel5 = value;
        }
    } = ConfigFactory.CurrentConfig.Toolbox.ChooseLevel5;

    /// <summary>
    /// Gets or sets a value indicating whether to choose level 6.
    /// </summary>
    public bool ChooseLevel6
    {
        get; set {
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Toolbox.ChooseLevel6 = value;
        }
    } = ConfigFactory.CurrentConfig.Toolbox.ChooseLevel6;

    [PropertyDependsOn(nameof(ChooseLevel3Time))]
    public int ChooseLevel3Hour
    {
        get => ChooseLevel3Time / 60;
        set => ChooseLevel3Time = (value * 60) + ChooseLevel3Min;
    }

    [PropertyDependsOn(nameof(ChooseLevel3Time))]
    public int ChooseLevel3Min
    {
        get => (ChooseLevel3Time % 60) / 10 * 10;
        set => ChooseLevel3Time = (ChooseLevel3Hour * 60) + value;
    }

    public int ChooseLevel3Time
    {
        get; set {
            value = value switch {
                < 60 => 9 * 60,
                > 9 * 60 => 60,
                _ => value / 10 * 10,
            };
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Toolbox.ChooseLevel3Time = value;
        }
    } = ConfigFactory.CurrentConfig.Toolbox.ChooseLevel3Time;

    [PropertyDependsOn(nameof(ChooseLevel4Time))]
    public int ChooseLevel4Hour
    {
        get => ChooseLevel4Time / 60;
        set => ChooseLevel4Time = (value * 60) + ChooseLevel4Min;
    }

    [PropertyDependsOn(nameof(ChooseLevel4Time))]
    public int ChooseLevel4Min
    {
        get => (ChooseLevel4Time % 60) / 10 * 10;
        set => ChooseLevel4Time = (ChooseLevel4Hour * 60) + value;
    }

    public int ChooseLevel4Time
    {
        get; set {
            value = value switch {
                < 60 => 9 * 60,
                > 9 * 60 => 60,
                _ => value / 10 * 10,
            };
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Toolbox.ChooseLevel4Time = value;
        }
    } = ConfigFactory.CurrentConfig.Toolbox.ChooseLevel4Time;

    /// <summary>
    /// Gets or sets a value indicating whether to set time automatically.
    /// </summary>
    public bool RecruitAutoSetTime
    {
        get; set {
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Toolbox.AutoSetTime = value;
        }
    } = ConfigFactory.CurrentConfig.Toolbox.AutoSetTime;

    /// <summary>
    /// Starts calculation.
    /// UI 缁戝畾鐨勬柟娉?
    /// </summary>
    /// <returns>Task</returns>
    [UsedImplicitly]
    public async Task RecruitStartCalc()
    {
        string errMsg = string.Empty;
        RecruitInfo = LocalizationHelper.GetString("ConnectingToEmulator");
        _runningState.SetIdle(false);
        var recruitCaught = await Task.Run(() => Instances.AsstProxy.AsstConnect(ref errMsg));
        if (!recruitCaught)
        {
            RecruitInfo = errMsg;
            _runningState.SetIdle(true);
            return;
        }

        RecruitInfo = LocalizationHelper.GetString("Identifying");

        var levelList = new List<int>();

        if (ChooseLevel3)
        {
            levelList.Add(3);
        }

        if (ChooseLevel4)
        {
            levelList.Add(4);
        }

        if (ChooseLevel5)
        {
            levelList.Add(5);
        }

        if (ChooseLevel6)
        {
            levelList.Add(6);
        }

        var task = new AsstRecruitTask() {
            SelectList = levelList,
            ConfirmList = [-1], // 浠呭叕鎷涜瘑鍒椂灏?1鍔犲叆comfirm_level
            SetRecruitTime = RecruitAutoSetTime,
            ChooseLevel3Time = ChooseLevel3Time,
            ChooseLevel4Time = ChooseLevel4Time,
            ServerType = Instances.SettingsViewModel.ServerType,
        };
        var (type, taskParams) = task.Serialize();
        bool ret = Instances.AsstProxy.AsstAppendTaskWithEncoding(AsstProxy.TaskType.RecruitCalc, type, taskParams);
        ret &= Instances.AsstProxy.AsstStart();
    }

    public bool RecruitmentShowPotential
    {
        get; set {
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Toolbox.ShowPotential = value;
        }
    } = ConfigFactory.CurrentConfig.Toolbox.ShowPotential;

    public void ProcRecruitMsg(JObject details)
    {
        string? what = details["what"]?.ToString();
        var subTaskDetails = details["details"];

        switch (what)
        {
            case "RecruitTagsDetected":
                {
                    JArray? tags = (JArray?)subTaskDetails?["tags"];
                    string infoContent = LocalizationHelper.GetString("RecruitTagsDetected");
                    tags ??= [];
                    infoContent = tags.Select(tagName => tagName.ToString()).Aggregate(infoContent, (current, tagStr) => current + (tagStr + "    "));

                    RecruitInfo = infoContent;
                }

                break;

            case "RecruitResult":
                {
                    JArray? resultArray = (JArray?)subTaskDetails?["result"];
                    UpdateRecruitResult(resultArray);
                }

                break;
        }
    }

    #endregion Recruit

    #region Depot

    private string _depotInfo = string.Empty;

    /// <summary>
    /// Gets or sets the depot info.
    /// </summary>
    public string DepotInfo
    {
        get => _depotInfo;
        set => SetAndNotify(ref _depotInfo, value);
    }

    /// <summary>
    /// Gets or sets 涓婃浠撳簱鍚屾鏃堕棿锛圲TC 鏃堕棿锛?
    /// </summary>
    public DateTimeOffset? LastDepotSyncTime { get => field; set => SetAndNotify(ref field, value); }

    /// <summary>
    /// Gets 涓婃浠撳簱鍚屾鏃堕棿鐨勬樉绀烘枃鏈紙鏈湴鏃堕棿锛?
    /// </summary>
    [PropertyDependsOn(nameof(LastDepotSyncTime))]
    public string LastDepotSyncTimeText
    {
        get {
            if (LastDepotSyncTime == null)
            {
                return string.Empty;
            }

            // 灏?UTC 鏃堕棿杞崲涓烘湰鍦版椂闂存樉绀?
            return LastDepotSyncTime.Value.ToLocalTimeString();
        }
    }

    private const int DepotRowSize = 5;

    private ObservableList<DepotResultDate> _depotResult = [];

    /// <summary>
    /// Gets or sets the depot result.
    /// </summary>
    public ObservableList<DepotResultDate> DepotResult
    {
        get => _depotResult;
        set {
            if (ReferenceEquals(_depotResult, value))
            {
                RefreshDepotRows();
                InvalidateDepotCache();
                return;
            }

            _depotResult.CollectionChanged -= DepotResultCollectionChanged;
            SetAndNotify(ref _depotResult, value);
            _depotResult.CollectionChanged += DepotResultCollectionChanged;
            RefreshDepotRows();
            InvalidateDepotCache();
        }
    }

    private ObservableCollection<ObservableCollection<DepotResultDate>> _depotRows = [];

    public ObservableCollection<ObservableCollection<DepotResultDate>> DepotRows
    {
        get => _depotRows;
        private set => SetAndNotify(ref _depotRows, value);
    }

    public int DepotColumnCount => GetColumnCount(DepotResult.Count, DepotRowSize);

    // 缂撳瓨鐩稿叧瀛楁
    private bool _depotCacheInvalid = true;
    private string? _cachedArkPlannerResult;
    private string? _cachedLoliconResult;
    private readonly HashSet<int> _pendingDepotSyncTimeResetTaskIds = [];

    public void MarkDepotRecognitionSyncTimeForReset(int taskId)
    {
        if (taskId > 0)
        {
            _pendingDepotSyncTimeResetTaskIds.Add(taskId);
        }
    }

    /// <summary>
    /// 鏍囪浠撳簱缂撳瓨澶辨晥
    /// </summary>
    private void InvalidateDepotCache()
    {
        _depotCacheInvalid = true;
        _cachedArkPlannerResult = null;
        _cachedLoliconResult = null;
    }

    public class DepotResultDate : IComparable<DepotResultDate>
    {
        public string? Name { get; set; }

        public string Id { get; set; } = null!;

        public BitmapSource? Image { get; set; }

        /// <summary>
        /// Gets or sets 鐗╁搧鏁伴噺锛堝師濮嬫暟鍊硷級
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets 鏍煎紡鍖栧悗鐨勬樉绀烘暟閲忥紙鐢ㄤ簬 UI 缁戝畾锛?
        /// </summary>
        public string? DisplayCount => Count >= 0 ? Count.FormatNumber(false) : null;

        /// <summary>
        /// 鍒涘缓鍚庢噿鍔犺浇缂撳瓨鐨?sortId锛岄伩鍏嶆瘡娆℃瘮杈冮兘鏌ュ瓧鍏搞€?
        /// </summary>
        private int? _cachedSortId;

        private int SortId => _cachedSortId ??=
            ItemListHelper.ArkItems != null &&
            ItemListHelper.ArkItems.TryGetValue(Id, out var item)
                ? item.SortId
                : int.MaxValue;

        /// <summary>
        /// 鎸夋父鎴忓唴缃?sortId 鎺掑簭锛堝€艰秺灏忚秺闈犲墠锛夛紝鏌ヤ笉鍒扮殑鎸?ID 鏂囨湰鍏滃簳銆?
        /// </summary>
        /// <param name="other">瑕佹瘮杈冪殑鍙︿竴涓?DepotResultDate 瀵硅薄</param>
        /// <returns>姣旇緝缁撴灉</returns>
        public int CompareTo(DepotResultDate? other)
        {
            if (other is null)
            {
                return 1;
            }

            if (SortId != other.SortId)
            {
                return SortId.CompareTo(other.SortId);
            }

            return string.Compare(Id, other.Id, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void InitializeDepotRowPresentation()
    {
        _depotResult.CollectionChanged += DepotResultCollectionChanged;
        RefreshDepotRows();
    }

    private void DepotResultCollectionChanged(in NotifyCollectionChangedEventArgs<DepotResultDate> e) => RefreshDepotRows();

    private void RefreshDepotRows()
    {
        DepotRows = BuildRows(DepotResult, DepotRowSize);
        NotifyOfPropertyChange(nameof(DepotColumnCount));
    }

    /// <summary>
    /// 淇濆瓨浠撳簱璇︽儏鏁版嵁
    /// </summary>
    private void SaveDepotDetails()
    {
        // 鏋勫缓绠€鍖栨牸寮忥細{"itemId": count}
        var details = new JObject {
            ["done"] = true,
            ["data"] = JObject.FromObject(DepotResult.Where(item => item.Count >= 0).ToDictionary(item => item.Id, item => item.Count)),
        };

        // feat/account-scoped-recognition-data: 记录数据所属账号原始名 (下拉显示用) - 见 partial class AccountScopedData
        StampDepotAccountField(details);

        // 淇濆瓨鍚屾鏃堕棿涓?UTC锛堝鏋滄湁锛?
        if (LastDepotSyncTime.HasValue)
        {
            details["syncTime"] = LastDepotSyncTime.Value.ToLocalTime().ToString("o"); // ISO 8601 鏍煎紡
        }

        // feat/account-scoped-recognition-data: 鎸夊綋鍓嶈处鍙锋《淇濆瓨
        JsonDataHelper.Set(DepotBucketKey, details);
        RegisterCurrentBucketIfNew();
    }

    private void LoadDepotDetails()
    {
        // feat/account-scoped-recognition-data: 浠庡綋鍓嶈处鍙锋《鍔犺浇
        var json = JsonDataHelper.Get(DepotBucketKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var details = JObject.Parse(json);
            DepotParse(details);
        }
        catch (Exception ex)
        {
            _logger.Error("parse depot json failed,\n{str}", json, ex);
        }
    }

    /// <summary>
    /// Gets 鑾峰彇 ArkPlanner 瀵煎嚭鏍煎紡锛堝甫缂撳瓨锛?
    /// </summary>
    public string ArkPlannerResult
    {
        get {
            if (DepotResult.Count == 0)
            {
                return string.Empty;
            }

            // 浣跨敤缂撳瓨
            if (!_depotCacheInvalid && _cachedArkPlannerResult != null)
            {
                return _cachedArkPlannerResult;
            }

            // 閲嶆柊璁＄畻
            var items = DepotResult
                .Where(item => item.Count >= 0)
                .Select(item => new JObject {
                    ["id"] = item.Id,
                    ["have"] = item.Count,
                    ["name"] = item.Name ?? string.Empty,
                });

            var result = new JObject {
                ["@type"] = "@penguin-statistics/depot",
                ["items"] = new JArray(items),
            };

            _cachedArkPlannerResult = result.ToString(Formatting.None);
            _depotCacheInvalid = false; // 鏍囪缂撳瓨宸叉洿鏂?
            return _cachedArkPlannerResult;
        }
    }

    /// <summary>
    /// Gets 鑾峰彇 宸ュ叿绠?瀵煎嚭鏍煎紡锛堝甫缂撳瓨锛?
    /// </summary>
    public string LoliconResult
    {
        get {
            if (DepotResult.Count == 0)
            {
                return string.Empty;
            }

            // 浣跨敤缂撳瓨
            if (!_depotCacheInvalid && _cachedLoliconResult != null)
            {
                return _cachedLoliconResult;
            }

            // 閲嶆柊璁＄畻
            var depotData = new JObject();
            foreach (var item in DepotResult)
            {
                if (item.Count >= 0)
                {
                    depotData[item.Id] = item.Count;
                }
            }

            _cachedLoliconResult = depotData.ToString(Formatting.None);
            _depotCacheInvalid = false; // 鏍囪缂撳瓨宸叉洿鏂?
            return _cachedLoliconResult;
        }
    }

    /// <summary>
    /// 瑙ｆ瀽浠撳簱璇嗗埆缁撴灉锛堝吋瀹规柊鏃ф牸寮忥級
    /// </summary>
    /// <param name="details">璇︾粏鐨?JSON 鍙傛暟</param>
    /// <param name="updateSyncTime">鏄惁鏇存柊鍚屾鏃堕棿涓哄綋鍓嶆椂闂达紙浠?Core 鑾峰彇鏂版暟鎹椂涓?true锛屼粠鏈湴鍔犺浇鏃朵负 false锛?/param>
    /// <param name="taskId">浼犲叆瀵瑰簲鐨勪换鍔?ID 浠ヤ究鍦ㄦ敹鍒板洖璋冨悗閲嶇疆璇嗗埆鐘舵€?/param>
    /// <returns>鏄惁鎴愬姛</returns>
    public bool DepotParse(JObject? details, bool updateSyncTime = false, int taskId = 0)
    {
        if (details == null)
        {
            return false;
        }

        if (_pendingDepotSyncTimeResetTaskIds.Remove(taskId))
        {
            ResetDepotRecognitionState();
        }

        DepotResult.Clear();

        Dictionary<string, int> depotItems = [];

        // 灏濊瘯瑙ｆ瀽鏂版牸寮?
        var dataToken = details["data"];
        if (dataToken is JObject dataObj)
        {
            foreach (var prop in dataObj.Properties())
            {
                if (int.TryParse(prop.Value.ToString(), out var count))
                {
                    depotItems[prop.Name] = count;
                }
            }
        }
        else if (dataToken?.ToString() is string dataStr && !string.IsNullOrEmpty(dataStr))
        { // 鏃х増鏍煎紡杩佺Щ
            try
            {
                var dataO = JObject.Parse(dataStr);
                foreach (var prop in dataO.Properties())
                {
                    if (int.TryParse(prop.Value.ToString(), out var count))
                    {
                        depotItems[prop.Name] = count;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to parse depot data format");
            }
        }

        // 濡傛灉鏂版牸寮忚В鏋愬け璐ワ紝灏濊瘯鏃ф牸寮?
        if (depotItems.Count == 0)
        {
            if (depotItems.Count == 0)
            {
                var arkplannerItems = details["arkplanner"]?["object"]?["items"]?.Cast<JObject>() ?? [];

                foreach (var item in arkplannerItems)
                {
                    var id = (string?)item["id"];
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    if (item["have"] != null && int.TryParse(item["have"]?.ToString(), out var count))
                    {
                        depotItems[id] = count;
                    }
                }
            }
        }

        // 鏋勫缓缁撴灉骞舵鏌ユ垚灏憋紝鎸夋父鎴忓唴缃?sortId 鎺掑簭
        var results = depotItems.Select(kvp => new DepotResultDate {
            Id = kvp.Key,
            Name = ItemListHelper.GetItemName(kvp.Key),
            Image = ItemListHelper.GetItemImage(kvp.Key),
            Count = kvp.Value,
        }).OrderBy(r => r);

        foreach (var result in results)
        {
            if (result.Count > 0 &&
                result.Count > AchievementTrackerHelper.Instance.GetProgress(AchievementIds.WarehouseMiser))
            {
                AchievementTrackerHelper.Instance.SetProgress(AchievementIds.WarehouseMiser, result.Count);
            }
        }

        DepotResult.AddRange(results);

        // 鏍囪缂撳瓨澶辨晥
        InvalidateDepotCache();

        bool done = (bool)(details["done"] ?? false);
        if (!done)
        {
            return true;
        }

        if (updateSyncTime)
        {
            // 浠?Core 鑾峰彇鏂版暟鎹紝鏇存柊涓哄綋鍓?UTC 鏃堕棿
            AchievementTrackerHelper.Instance.CheckResyncAfterDays(LastDepotSyncTime?.UtcDateTime, 7, AchievementIds.ResumeRecord);
            LastDepotSyncTime = DateTimeOffset.UtcNow;
        }
        else
        {
            // 浠庢湰鍦板姞杞斤紝璇诲彇淇濆瓨鐨勬椂闂?
            var syncTimeStr = details["syncTime"]?.ToString(Formatting.None)?.Trim('"');
            if (!string.IsNullOrEmpty(syncTimeStr) && DateTimeOffset.TryParse(syncTimeStr, null, DateTimeStyles.AssumeUniversal, out var lastDepotSyncTime))
            {
                LastDepotSyncTime = lastDepotSyncTime;
            }
        }

        DepotInfo = LocalizationHelper.GetString("IdentificationCompleted");
        SaveDepotDetails();
        Instances.TaskQueueViewModel.UpdateDatePrompt();

        return true;
    }

    /// <summary>
    /// 浠撳簱瀵煎嚭鏍煎紡
    /// </summary>
    public enum DepotExportFormat
    {
        /// <summary>
        /// https://github.com/penguin-statistics/ArkPlanner
        /// </summary>
        Arkplanner = 0,

        /// <summary>
        /// https://arkntools.app/#/material
        /// </summary>
        Lolicon = 1,

        /// <summary>
        /// Specifies that the content is formatted using Markdown syntax.
        /// </summary>
        Markdown = 2,

        /// <summary>
        /// Specifies that the content is formatted as comma-separated values (CSV).
        /// </summary>
        Csv = 3,
    }

    /// <summary>
    /// 骞插憳BOX瀵煎嚭鏍煎紡
    /// </summary>
    public enum OperBoxExportFormat
    {
        /// <summary>
        /// Represents the clipboard as a data source or destination.
        /// </summary>
        Clipboard = 0,

        /// <summary>
        /// Specifies that the content type is JSON format.
        /// </summary>
        Json = 1,

        /// <summary>
        /// Specifies that the content is formatted using Markdown syntax.
        /// </summary>
        Markdown = 2,

        /// <summary>
        /// Specifies that the data format is comma-separated values (CSV).
        /// </summary>
        Csv = 3,
    }

    public record struct ExportEntry(string Display, int Value);

    public List<ExportEntry> ExportOptionList { get; } = [
        new(LocalizationHelper.GetString("ExportToArkplanner"), (int)DepotExportFormat.Arkplanner),
        new(LocalizationHelper.GetString("ExportToLolicon"), (int)DepotExportFormat.Lolicon),
        new(LocalizationHelper.GetString("ExportToMarkdown"), (int)DepotExportFormat.Markdown),
        new(LocalizationHelper.GetString("ExportToCsv"), (int)DepotExportFormat.Csv),
    ];

    private int _selectedExportValue;

    public int SelectedExportValue
    {
        get => _selectedExportValue;
        set => SetAndNotify(ref _selectedExportValue, value);
    }

    [UsedImplicitly]
    public void ExecuteSelectedExport()
    {
        switch ((DepotExportFormat)_selectedExportValue)
        {
            case DepotExportFormat.Arkplanner: ExportToArkplanner(); break;
            case DepotExportFormat.Lolicon: ExportToLolicon(); break;
            case DepotExportFormat.Markdown: ExportToMarkdown(); break;
            case DepotExportFormat.Csv: ExportToCsv(); break;
        }
    }

    /// <summary>
    /// Export depot info to ArkPlanner.
    /// UI 缁戝畾鐨勬柟娉?
    /// </summary>
    [UsedImplicitly]
    public void ExportToArkplanner()
    {
        Clipboard.Clear();
        Clipboard.SetDataObject(ArkPlannerResult);
        Growl.Info(LocalizationHelper.GetString("CopiedToClipboard"));
    }

    /// <summary>
    /// Export depot info to Lolicon.
    /// UI 缁戝畾鐨勬柟娉?
    /// </summary>
    [UsedImplicitly]
    public void ExportToLolicon()
    {
        Clipboard.Clear();
        Clipboard.SetDataObject(LoliconResult);
        Growl.Info(LocalizationHelper.GetString("CopiedToClipboard"));
    }

    /// <summary>
    /// Export depot info to Markdown file.
    /// UI 缁戝畾鐨勬柟娉?
    /// </summary>
    [UsedImplicitly]
    public void ExportToMarkdown()
    {
        ExportDepot(BuildMarkdownExportLines, "Markdown files (*.md)|*.md|All files (*.*)|*.*", ".md", "Arknights_Depot_Export.md");
    }

    /// <summary>
    /// Export depot info to CSV file.
    /// UI 缁戝畾鐨勬柟娉?
    /// </summary>
    [UsedImplicitly]
    public void ExportToCsv()
    {
        ExportDepot(BuildCsvExportLines, "CSV files (*.csv)|*.csv|All files (*.*)|*.*", ".csv", "Arknights_Depot_Export.csv");
    }

    private void ExportDepot(Func<IReadOnlyList<DepotResultDate>, IEnumerable<string>> lineBuilder, string filter, string defaultExt, string defaultFileName)
    {
        var items = DepotResult.Where(item => item.Count >= 0).ToList();
        if (items.Count == 0)
        {
            return;
        }

        var content = string.Join(Environment.NewLine, lineBuilder(items));

        var dialog = new Microsoft.Win32.SaveFileDialog {
            Filter = filter,
            DefaultExt = defaultExt,
            FileName = defaultFileName,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, content);
        Growl.Info(LocalizationHelper.GetString("ExportedToFile"));
    }

    private static IEnumerable<string> BuildMarkdownExportLines(IReadOnlyList<DepotResultDate> items)
    {
        var lines = new List<string> { "# Arknights Depot Export", string.Empty, "| ID | Name | Count |", "| --- | --- | --- |" };
        foreach (var item in items)
        {
            lines.Add($"| {item.Id} | {item.Name ?? string.Empty} | {item.Count} |");
        }

        return lines;
    }

    private static IEnumerable<string> BuildCsvExportLines(IReadOnlyList<DepotResultDate> items)
    {
        var lines = new List<string> { "ID,Name,Count" };
        foreach (var item in items)
        {
            var name = item.Name ?? string.Empty;
            if (name.Contains(',') || name.Contains('"') || name.Contains('\n'))
            {
                name = "\"" + name.Replace("\"", "\"\"") + "\"";
            }

            lines.Add($"{item.Id},{name},{item.Count}");
        }

        return lines;
    }

    /*
    private void DepotClear()
    {
        DepotResult.Clear();
        InvalidateDepotCache();
    }
    */

    // 闇€瑕佹帓闄ょ殑鐗╁搧 ID锛堜笉缁熻鍒颁粨搴擄級
    private static readonly HashSet<string> ExcludedItemIds =
    [
        "3401", // 瀹跺叿
        "3112", "3113", "3114", // 纰?
        "5001", // 缁忛獙
    ];

    /// <summary>
    /// 妫€鏌ョ墿鍝?ID 鏄惁搴旇琚帓闄わ紙涓嶇粺璁″埌浠撳簱锛?
    /// </summary>
    /// <param name="itemId">鐗╁搧 ID</param>
    /// <returns>true 琛ㄧず搴旇鎺掗櫎</returns>
    private static bool ShouldExcludeItem(string itemId)
    {
        // 鎺掗櫎鐗瑰畾 ID
        if (ExcludedItemIds.Contains(itemId))
        {
            return true;
        }

        // 鎺掗櫎闈炵函鏁板瓧鐨?ID
        if (!int.TryParse(itemId, out _))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 鏍规嵁 StageDrops 鏁版嵁鏇存柊浠撳簱
    /// </summary>
    /// <param name="drops">鍏冲崱鎺夎惤鏁版嵁鍒楄〃 (ItemId, ItemName, Total, Add)</param>
    public void UpdateDepotFromDrops(List<(string ItemId, string ItemName, int Total, int Add)> drops)
    {
        if (drops == null || drops.Count == 0)
        {
            return;
        }

        // feat/account-scoped-recognition-data: 褰撳墠璐﹀彿妗舵棤鍩虹嚎 (浠庢湭浠撳簱璇嗗埆) 鏃朵涪寮冩帀钀藉閲?
        // 闃叉鍗曞眬鎺夎惤琚褰撳簱瀛樺熀鏁? 浠ュ強杞崲鍒囧彿鍚庝互涓婁竴璐﹀彿搴撳瓨涓哄熀鏁扮殑璺ㄨ处鍙锋暟鎹悎骞?
        if (ShouldSkipDepotDropsForEmptyBucket())
        {
            return;
        }

        bool hasUpdates = false;

        foreach (var (itemId, _, total, add) in drops)
        {
            if (string.IsNullOrEmpty(itemId) || add <= 0)
            {
                continue;
            }

            // 杩囨护涓嶉渶瑕佺粺璁＄殑鐗╁搧
            if (ShouldExcludeItem(itemId))
            {
                continue;
            }

            // 鏌ユ壘鐜版湁浠撳簱椤?
            var existingItem = DepotResult.FirstOrDefault(x => x.Id == itemId);
            if (existingItem != null)
            {
                // 鏇存柊鐜版湁鐗╁搧鏁伴噺
                if (existingItem.Count >= 0)
                {
                    var newCount = existingItem.Count + add;
                    existingItem.Count = newCount;
                    hasUpdates = true;

                    // 鏇存柊鎴愬氨杩涘害
                    if (newCount > AchievementTrackerHelper.Instance.GetProgress(AchievementIds.WarehouseMiser))
                    {
                        AchievementTrackerHelper.Instance.SetProgress(AchievementIds.WarehouseMiser, newCount);
                    }
                }
            }
            else
            {
                // 娣诲姞鏂扮墿鍝?
                var newItem = new DepotResultDate {
                    Id = itemId,
                    Name = ItemListHelper.GetItemName(itemId),
                    Image = ItemListHelper.GetItemImage(itemId),
                    Count = add,
                };
                DepotResult.Add(newItem);
                hasUpdates = true;
            }
        }

        // 濡傛灉鏈夋洿鏂帮紝閲嶆柊鎺掑簭骞朵繚瀛?
        if (hasUpdates)
        {
            // 鎸夋父鎴忓唴缃?sortId 鎺掑簭
            DepotResult.Sort();

            // 鏍囪缂撳瓨澶辨晥
            InvalidateDepotCache();

            // 淇濆瓨鏇存柊鍚庣殑鏁版嵁
            SaveDepotDetails();
            Instances.TaskQueueViewModel.UpdateDatePrompt();

            _logger.Information("Depot updated from stage drops, {Count} items processed", drops.Count);
        }
    }

    /// <summary>
    /// 閲嶇疆浠撳簱璇嗗埆鐘舵€併€?
    /// </summary>
    public void ResetDepotRecognitionState()
    {
        // DepotParse 鏂规硶宸茬粡澶勭悊浜嗘暟鎹竻闄ゅ拰缂撳瓨澶辨晥锛岃繖閲屼笉闇€瑕侀噸澶嶈皟鐢?
        // DepotClear();
        LastDepotSyncTime = null;
    }

    /// <summary>
    /// 杩藉姞鎴栧惎鍔ㄤ粨搴撹瘑鍒换鍔°€?
    /// </summary>
    /// <param name="startImmediately">鏄惁绔嬪埢鍚姩銆?/param>
    /// <returns>鏄惁鎴愬姛銆?/returns>
    public bool StartDepotRecognitionTask(bool startImmediately = true)
    {
        bool ret = Instances.AsstProxy.AsstStartDepot(startImmediately);
        if (ret && startImmediately)
        {
            DepotInfo = LocalizationHelper.GetString("Identifying");
        }

        return ret;
    }

    /// <summary>
    /// Starts depot recognition.
    /// UI 缁戝畾鐨勬柟娉?
    /// </summary>
    /// <returns>Task</returns>
    [UsedImplicitly]
    public async Task StartDepot()
    {
        // feat/account-scoped-recognition-data: 鎵嬪姩璇嗗埆鍓嶉敋瀹氬洖閰嶇疆璐﹀彿妗? 闃叉鍐欏叆鏌ョ湅涓殑鍏朵粬璐﹀彿妗?
        SwitchDataAccount(ResolveConfiguredAccountName());
        _runningState.SetIdle(false);
        string errMsg = string.Empty;
        DepotInfo = LocalizationHelper.GetString("ConnectingToEmulator");
        bool caught = await Task.Run(() => Instances.AsstProxy.AsstConnect(ref errMsg));
        if (!caught)
        {
            DepotInfo = errMsg;
            _runningState.SetIdle(true);
            return;
        }

        ResetDepotRecognitionState();
        StartDepotRecognitionTask();
    }

    #endregion Depot

    #region OperBox

    /// <summary>
    /// Gets or sets 涓婃骞插憳鍚屾鏃堕棿
    /// </summary>
    public DateTimeOffset? LastOperBoxSyncTime { get => field; set => SetAndNotify(ref field, value); }

    /// <summary>
    /// Gets 涓婃骞插憳鍚屾鏃堕棿鐨勬樉绀烘枃鏈?
    /// </summary>
    [PropertyDependsOn(nameof(LastOperBoxSyncTime))]
    public string LastOperBoxSyncTimeText
    {
        get {
            if (LastOperBoxSyncTime == null)
            {
                return string.Empty;
            }

            return LastOperBoxSyncTime.Value.ToLocalTimeString();
        }
    }

    private int _operBoxSelectedIndex = 0;

    public int OperBoxSelectedIndex
    {
        get => _operBoxSelectedIndex;
        set => SetAndNotify(ref _operBoxSelectedIndex, value);
    }

    private string _operBoxInfo = LocalizationHelper.GetString("OperBoxRecognitionTip");

    public string OperBoxInfo
    {
        get => _operBoxInfo;
        set => SetAndNotify(ref _operBoxInfo, value);
    }

    /// <summary>
    /// Gets OperBoxDataArray from OperBoxHaveList for backward compatibility
    /// </summary>
    [Obsolete("Use OperBoxHaveList instead")]
    public List<OperBoxData.OperData> OperBoxDataArray
    {
        get => [.. OperBoxHaveList.Select(op => new OperBoxData.OperData {
            Id = op.Id,
            Name = op.Name,
            Rarity = op.Rarity,
            Elite = op.Elite,
            Level = op.Level,
            Potential = op.Potential,
            Own = true,
        })];
    }

    private Dictionary<string, int>? _operBoxPotential;

    public Dictionary<string, int>? OperBoxPotential
    {
        get {
            if (_operBoxPotential != null)
            {
                return _operBoxPotential;
            }

            _operBoxPotential = OperBoxHaveList.ToDictionary(oper => oper.Id, oper => oper.Potential);
            return _operBoxPotential;
        }
    }

    public class Operator(string id, string name, int rarity, int elite = 0, int level = 0, int potential = 0)
    {
        [JsonProperty("id")]
        public string Id { get; } = id;

        [JsonProperty("name")]
        public string Name { get; } = name;

        [JsonProperty("rarity")]
        public int Rarity { get; } = rarity;

        [JsonProperty("elite")]
        public int Elite { get; } = elite;

        [JsonProperty("level")]
        public int Level { get; } = level;

        [JsonProperty("potential")]
        public int Potential { get; } = potential;

        public int IdNumber { get; } = ExtractIdNumber(id);

        public string RarityStars => (IsPallas && Level > 0) ? LocalizationHelper.GetPallasString(6, 6) : new('★', Rarity);

        /// <summary>
        /// Gets the path to the Elite icon image
        /// </summary>
        public string EliteIconPath => $"/Res/Img/Operator/Elite_{Elite}.png";

        /// <summary>
        /// Gets the path to the Potential icon image
        /// </summary>
        public string PotentialIconPath => Potential > 0 && Potential <= 6
            ? $"/Res/Img/Operator/Potential_{Potential}.png"
            : "/Res/Img/Operator/Potential_1.png";

        /// <summary>
        /// Gets the resource key based on rarity
        /// </summary>
        public string RarityColorResourceKey => (IsPallas && Level > 0) ? "AchievementBrush.Rare.LinearGradientBrush" : $"Star{Rarity}OperatorLogBrush";

        public bool Equals(Operator? other) => other != null && Name == other.Name && Rarity == other.Rarity;

        public override bool Equals(object? obj) => obj is Operator other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Id, Name, Rarity);

        public override string ToString() => $"{Name} (★{Rarity})";

        public bool IsPallas => Id == "char_485_pallas";

        private static int ExtractIdNumber(string id)
        {
            // Expected format: "char_002_amiya"
            if (string.IsNullOrEmpty(id))
            {
                return 0;
            }

            int firstUnderscore = id.IndexOf('_');
            if (firstUnderscore < 0 || firstUnderscore >= id.Length - 1)
            {
                return 0;
            }

            int secondUnderscore = id.IndexOf('_', firstUnderscore + 1);
            if (secondUnderscore < 0 || secondUnderscore <= firstUnderscore + 1)
            {
                return 0;
            }

            ReadOnlySpan<char> numericSpan = id.AsSpan(firstUnderscore + 1, secondUnderscore - firstUnderscore - 1);
            if (int.TryParse(numericSpan, out int value))
            {
                return value;
            }

            return 0;
        }
    }

    private const int OperBoxRowSize = 5;

    private ObservableCollection<Operator> _operBoxHaveList = [];

    public ObservableCollection<Operator> OperBoxHaveList
    {
        get => _operBoxHaveList;
        set {
            if (ReferenceEquals(_operBoxHaveList, value))
            {
                RefreshOperBoxHaveRows();
                return;
            }

            _operBoxHaveList.CollectionChanged -= OperBoxHaveListCollectionChanged;
            SetAndNotify(ref _operBoxHaveList, value);
            _operBoxHaveList.CollectionChanged += OperBoxHaveListCollectionChanged;
            RefreshOperBoxHaveRows();
        }
    }

    private ObservableCollection<ObservableCollection<Operator>> _operBoxHaveRows = [];

    public ObservableCollection<ObservableCollection<Operator>> OperBoxHaveRows
    {
        get => _operBoxHaveRows;
        private set => SetAndNotify(ref _operBoxHaveRows, value);
    }

    public int OperBoxHaveColumnCount => GetColumnCount(OperBoxHaveList.Count, OperBoxRowSize);

    private ObservableCollection<Operator> _operBoxNotHaveList = [];

    public ObservableCollection<Operator> OperBoxNotHaveList
    {
        get => _operBoxNotHaveList;
        set {
            if (ReferenceEquals(_operBoxNotHaveList, value))
            {
                RefreshOperBoxNotHaveRows();
                return;
            }

            _operBoxNotHaveList.CollectionChanged -= OperBoxNotHaveListCollectionChanged;
            SetAndNotify(ref _operBoxNotHaveList, value);
            _operBoxNotHaveList.CollectionChanged += OperBoxNotHaveListCollectionChanged;
            RefreshOperBoxNotHaveRows();
        }
    }

    private ObservableCollection<ObservableCollection<Operator>> _operBoxNotHaveRows = [];

    public ObservableCollection<ObservableCollection<Operator>> OperBoxNotHaveRows
    {
        get => _operBoxNotHaveRows;
        private set => SetAndNotify(ref _operBoxNotHaveRows, value);
    }

    public int OperBoxNotHaveColumnCount => GetColumnCount(OperBoxNotHaveList.Count, OperBoxRowSize);

    private void InitializeOperBoxRowPresentation()
    {
        _operBoxHaveList.CollectionChanged += OperBoxHaveListCollectionChanged;
        _operBoxNotHaveList.CollectionChanged += OperBoxNotHaveListCollectionChanged;
        RefreshOperBoxHaveRows();
        RefreshOperBoxNotHaveRows();
    }

    private void OperBoxHaveListCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshOperBoxHaveRows();
    }

    private void OperBoxNotHaveListCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshOperBoxNotHaveRows();
    }

    private void RefreshOperBoxHaveRows()
    {
        OperBoxHaveRows = BuildRows(OperBoxHaveList, OperBoxRowSize);
        NotifyOfPropertyChange(nameof(OperBoxHaveColumnCount));
    }

    private void RefreshOperBoxNotHaveRows()
    {
        OperBoxNotHaveRows = BuildRows(OperBoxNotHaveList, OperBoxRowSize);
        NotifyOfPropertyChange(nameof(OperBoxNotHaveColumnCount));
    }

    private static ObservableCollection<ObservableCollection<T>> BuildRows<T>(IEnumerable<T> items, int rowSize)
    {
        ObservableCollection<ObservableCollection<T>> rows = [];

        foreach (var row in items.Chunk(rowSize))
        {
            rows.Add(new ObservableCollection<T>(row));
        }

        return rows;
    }

    private static int GetColumnCount(int count, int rowSize)
    {
        return count <= 0 ? 1 : Math.Min(count, rowSize);
    }

    private void SaveOperBoxDetails(List<OperBoxData.OperData> details)
    {
        var data = new JObject {
            ["done"] = true,
            ["own_opers"] = JArray.FromObject(details),
        };

        // feat/account-scoped-recognition-data: 璁板綍鏁版嵁鎵€灞炶处鍙峰師濮嬪悕 (涓嬫媺鏄剧ず鐢?
        StampOperBoxAccountField(data);

        if (LastOperBoxSyncTime.HasValue)
        {
            data["syncTime"] = LastOperBoxSyncTime.Value.ToLocalTime().ToString("o");
        }

        // feat/account-scoped-recognition-data: 鎸夊綋鍓嶈处鍙锋《淇濆瓨
        JsonDataHelper.Set(OperBoxBucketKey, data);
        RegisterCurrentBucketIfNew();
    }

    private void SortOperBoxLists()
    {
        OperBoxHaveList = SortOperBoxList(OperBoxHaveList);
        OperBoxNotHaveList = SortOperBoxList(OperBoxNotHaveList);
    }

    private static ObservableCollection<Operator> SortOperBoxList(ObservableCollection<Operator> list)
    {
        if (list == null || list.Count <= 0)
        {
            return list ?? [];
        }

        return [.. list
            .OrderByDescending(x => x.IsPallas && x.Level > 0)
            .ThenByDescending(x => x.Rarity)
            .ThenByDescending(x => x.Elite)
            .ThenByDescending(x => x.Level)
            .ThenByDescending(x => x.Potential)
            .ThenByDescending(x => x.IdNumber)];
    }

    private void LoadOperBoxDetails()
    {
        // TODO: 鍒犻櫎鑰佹暟鎹妭鐪?gui.json 鐨勫ぇ灏忥紝鍚庣画鐗堟湰鍙互鍒犻櫎
        // var json = ConfigurationHelper.GetValue(ConfigurationKeys.OperBoxData, string.Empty);
        // feat/account-scoped-recognition-data: 浠庡綋鍓嶈处鍙锋《鍔犺浇
        var json = JsonDataHelper.Get(OperBoxBucketKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var token = JToken.Parse(json);
            if (token is JArray oldArray)
            {
                var ownOpers = oldArray
                    .ToObject<List<OperBoxData.OperData>>()?
                    .Where(i => !string.IsNullOrEmpty(i.Id))
                    .ToList();
                if (ownOpers is null)
                {
                    return;
                }
                LoadOperBoxList(ownOpers);
                return;
            }
            else if (token is JObject details)
            {
                OperBoxParse(details, updateSyncTime: false);
            }
        }
        catch
        {
            // 鍏煎鑰佹暟鎹垨寮傚父鏃跺拷鐣?
        }

        void LoadOperBoxList(List<OperBoxData.OperData> ownOpers)
        {
            var operDataMap = ownOpers.ToDictionary(o => o.Id);
            foreach (var (id, oper) in DataHelper.Operators)
            {
                if (!DataHelper.IsCharacterAvailableInClient(oper, SettingsViewModel.GameSettings.ClientType.ToCustomString()))
                {
                    continue;
                }

                var name = DataHelper.GetLocalizedCharacterName(oper) ?? "???";
                if (operDataMap.TryGetValue(id, out var operData))
                {
                    OperBoxHaveList.Add(new Operator(id, name, oper.Rarity, operData.Elite, operData.Level, operData.Potential));

                    if (id == "char_485_pallas")
                    {
                        AchievementTrackerHelper.Instance.Unlock(AchievementIds.WarehouseKeeper);
                    }
                }
                else
                {
                    OperBoxNotHaveList.Add(new Operator(id, name, oper.Rarity));
                }
            }

            SortOperBoxLists();
        }
    }

    /// <summary>
    /// 姣忔浼犺繘鏉ョ殑閮芥槸瀹屾暣鏁版嵁, 涓存椂缂撳瓨鍘婚噸
    /// </summary>
    private HashSet<string> _tempOperHaveSet = [];
    private readonly HashSet<int> _pendingOperBoxRecognitionResetTaskIds = [];

    public void MarkOperBoxRecognitionDataForReset(int taskId)
    {
        if (taskId > 0)
        {
            _pendingOperBoxRecognitionResetTaskIds.Add(taskId);
        }
    }

    private void ClearOperBoxRecognitionData()
    {
        OperBoxSelectedIndex = 1;
        _operBoxPotential = null;
        _tempOperHaveSet = [];
        OperBoxHaveList = [];
        OperBoxNotHaveList = [];
        LastOperBoxSyncTime = null;
    }

    /// <summary>
    /// 瑙ｆ瀽骞插憳璇嗗埆缁撴灉
    /// </summary>
    /// <param name="details">鏂板鐨勫共鍛樻暟鎹?/param>
    /// <param name="updateSyncTime">鏄惁鏇存柊鍚屾鏃堕棿锛堜粠 Core 鑾峰彇鏂版暟鎹椂涓?true锛屼粠鏈湴鍔犺浇鏃朵负 false锛?/param>
    /// <param name="taskId">浼犲叆瀵瑰簲鐨勪换鍔?ID 浠ヤ究鍦ㄦ敹鍒板洖璋冨悗閲嶇疆璇嗗埆鐘舵€?/param>
    /// <returns>鏄惁鎴愬姛</returns>
    public bool OperBoxParse(JObject? details, bool updateSyncTime = true, int taskId = 0)
    {
        if (details == null)
        {
            return false;
        }

        if (_pendingOperBoxRecognitionResetTaskIds.Remove(taskId))
        {
            ResetOperBoxRecognitionState();
        }

        var ownOpers = (details["own_opers"] as JArray)?.ToObject<List<OperBoxData.OperData>>()?.Where(o => !string.IsNullOrEmpty(o.Id)).ToList();
        if (ownOpers is null)
        {
            return false;
        }

        foreach (var oper in ownOpers)
        {
            if (_tempOperHaveSet.Add(oper.Id))
            {
                var name = DataHelper.GetLocalizedCharacterName(DataHelper.Operators.FirstOrDefault(i => i.Key == oper.Id).Value) ?? "???";
                OperBoxHaveList.Add(new Operator(oper.Id, name, oper.Rarity, oper.Elite, oper.Level, oper.Potential));
                if (oper.Id == "char_485_pallas")
                {
                    AchievementTrackerHelper.Instance.Unlock(AchievementIds.WarehouseKeeper);
                }
            }
        }

        bool done = (bool)(details["done"] ?? false);
        if (!done)
        {
            return true;
        }

        foreach (var (id, oper) in DataHelper.Operators)
        {
            if (!_tempOperHaveSet.Contains(id) && DataHelper.IsCharacterAvailableInClient(oper, SettingsViewModel.GameSettings.ClientType.ToCustomString()))
            {
                var name = DataHelper.GetLocalizedCharacterName(oper) ?? "???";
                OperBoxNotHaveList.Add(new Operator(id, name, oper.Rarity));
            }
        }

        SortOperBoxLists();

        if (OperBoxNotHaveList.Count > 0)
        {
            OperBoxSelectedIndex = 0;
        }

        if (updateSyncTime)
        {
            AchievementTrackerHelper.Instance.CheckResyncAfterDays(LastOperBoxSyncTime?.UtcDateTime, 7, AchievementIds.ResumeRecord);
            LastOperBoxSyncTime = DateTimeOffset.UtcNow;
        }
        else
        {
            var syncTimeStr = details["syncTime"]?.ToString(Formatting.None)?.Trim('"');
            if (!string.IsNullOrEmpty(syncTimeStr) && DateTimeOffset.TryParse(syncTimeStr, null, DateTimeStyles.AssumeUniversal, out var lastOperBoxSyncTime))
            {
                LastOperBoxSyncTime = lastOperBoxSyncTime;
            }
        }

        OperBoxInfo = $"{LocalizationHelper.GetString("IdentificationCompleted")}  {LocalizationHelper.GetString("OperBoxRecognitionTip")}";
        SaveOperBoxDetails(ownOpers);
        _tempOperHaveSet = [];
        return true;
    }

    /// <summary>
    /// 閲嶇疆骞插憳璇嗗埆鐘舵€併€?
    /// </summary>
    public void ResetOperBoxRecognitionState()
    {
        ClearOperBoxRecognitionData();
        LastOperBoxSyncTime = null;
    }

    /// <summary>
    /// 杩藉姞鎴栧惎鍔ㄥ共鍛樿瘑鍒换鍔°€?
    /// </summary>
    /// <param name="startImmediately">鏄惁绔嬪埢鍚姩銆?/param>
    /// <returns>鏄惁鎴愬姛銆?/returns>
    public bool StartOperBoxRecognitionTask(bool startImmediately = true)
    {
        bool ret = Instances.AsstProxy.AsstStartOperBox(startImmediately);
        if (ret && startImmediately)
        {
            OperBoxInfo = LocalizationHelper.GetString("Identifying");
        }

        return ret;
    }

    /// <summary>
    /// 寮€濮嬭瘑鍒共鍛?
    /// UI 缁戝畾鐨勬柟娉?
    /// </summary>
    /// <returns>Task</returns>
    [UsedImplicitly]
    public async Task StartOperBox()
    {
        // feat/account-scoped-recognition-data: 鎵嬪姩璇嗗埆鍓嶉敋瀹氬洖閰嶇疆璐﹀彿妗? 闃叉鍐欏叆鏌ョ湅涓殑鍏朵粬璐﹀彿妗?
        SwitchDataAccount(ResolveConfiguredAccountName());
        ResetOperBoxRecognitionState();
        _runningState.SetIdle(false);
        string errMsg = string.Empty;
        OperBoxInfo = LocalizationHelper.GetString("ConnectingToEmulator");
        bool caught = await Task.Run(() => Instances.AsstProxy.AsstConnect(ref errMsg));
        if (!caught)
        {
            OperBoxInfo = errMsg;
            _runningState.SetIdle(true);
            return;
        }

        StartOperBoxRecognitionTask();
    }

    public List<GenericCombinedData<OperBoxExportFormat>> OperBoxExportOptionList { get; } = [
        new(LocalizationHelper.GetString("OperBoxExportToClipboard"), OperBoxExportFormat.Clipboard),
        new(LocalizationHelper.GetString("OperBoxExportToJson"), OperBoxExportFormat.Json),
        new(LocalizationHelper.GetString("ExportToMarkdown"), OperBoxExportFormat.Markdown),
        new(LocalizationHelper.GetString("ExportToCsv"), OperBoxExportFormat.Csv),
    ];

    public OperBoxExportFormat SelectedOperBoxExportValue
    {
        get; set {
            SetAndNotify(ref field, value);
            ConfigFactory.CurrentConfig.Toolbox.OperBoxExportFormat = value;
        }
    } = ConfigFactory.CurrentConfig.Toolbox.OperBoxExportFormat;

    // UI 缁戝畾鐨勬柟娉?
    [UsedImplicitly]
    public void ExportOperBox()
    {
        switch (SelectedOperBoxExportValue)
        {
            case OperBoxExportFormat.Clipboard: ExportOperBoxToClipboard(); break;
            case OperBoxExportFormat.Json: ExportOperBoxToJson(); break;
            case OperBoxExportFormat.Markdown: ExportOperBoxToMarkdown(); break;
            case OperBoxExportFormat.Csv: ExportOperBoxToCsv(); break;
        }
    }

    private List<OperBoxData.OperData> BuildOperBoxExportList()
    {
        if (OperBoxHaveList.Count == 0)
        {
            return [];
        }

        var exportList = new List<OperBoxData.OperData>();
        var userOperMap = OperBoxHaveList.ToDictionary(op => op.Id);

        foreach (var (operId, operInfo) in DataHelper.Operators)
        {
            if (!DataHelper.IsCharacterAvailableInClient(operInfo, SettingsViewModel.GameSettings.ClientType.ToCustomString()))
            {
                continue;
            }

            var operName = DataHelper.GetLocalizedCharacterName(operInfo) ?? "???";
            if (userOperMap.TryGetValue(operId, out var value))
            {
                exportList.Add(new OperBoxData.OperData() {
                    Id = value.Id,
                    Name = value.Name,
                    Rarity = value.Rarity,
                    Elite = value.Elite,
                    Level = value.Level,
                    Potential = value.Potential,
                    Own = true,
                });
            }
            else
            {
                exportList.Add(new OperBoxData.OperData() {
                    Id = operId,
                    Name = operName,
                    Rarity = operInfo.Rarity,
                    Own = false,
                });
            }
        }

        return exportList;
    }

    private void ExportOperBoxToClipboard()
    {
        var exportList = BuildOperBoxExportList();
        if (exportList.Count == 0)
        {
            return;
        }

        Clipboard.Clear();
        Clipboard.SetDataObject(JsonConvert.SerializeObject(exportList, Formatting.Indented));
        Growl.Info(LocalizationHelper.GetString("CopiedToClipboard"));
        AchievementTrackerHelper.Instance.Unlock(AchievementIds.OperatorRoster);
    }

    private void ExportOperBoxToFile(Func<IReadOnlyList<OperBoxData.OperData>, string> contentBuilder, string filter, string defaultExt, string defaultFileName)
    {
        var exportList = BuildOperBoxExportList();
        if (exportList.Count == 0)
        {
            return;
        }

        var content = contentBuilder(exportList);

        var dialog = new Microsoft.Win32.SaveFileDialog {
            Filter = filter,
            DefaultExt = defaultExt,
            FileName = defaultFileName,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, content, new UTF8Encoding(true));
        Growl.Info(LocalizationHelper.GetString("ExportedToFile"));
        AchievementTrackerHelper.Instance.Unlock(AchievementIds.OperatorRoster);
    }

    private void ExportOperBoxToJson()
    {
        ExportOperBoxToFile(
            list => JsonConvert.SerializeObject(list, Formatting.Indented),
            "JSON files (*.json)|*.json|All files (*.*)|*.*",
            ".json",
            "Arknights_OperBox_Export.json");
    }

    private void ExportOperBoxToMarkdown()
    {
        ExportOperBoxToFile(
            list => string.Join(Environment.NewLine, BuildOperBoxMarkdownExportLines(list)),
            "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            ".md",
            "Arknights_OperBox_Export.md");
    }

    private void ExportOperBoxToCsv()
    {
        ExportOperBoxToFile(
            list => string.Join(Environment.NewLine, BuildOperBoxCsvExportLines(list)),
            "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            ".csv",
            "Arknights_OperBox_Export.csv");
    }

    private static IEnumerable<string> BuildOperBoxMarkdownExportLines(IReadOnlyList<OperBoxData.OperData> items)
    {
        var nameHeader = LocalizationHelper.GetString("OperBoxExportHeaderName");
        var idHeader = LocalizationHelper.GetString("OperBoxExportHeaderId");
        var rarityHeader = LocalizationHelper.GetString("OperBoxExportHeaderRarity");
        var eliteHeader = LocalizationHelper.GetString("OperBoxExportHeaderElite");
        var levelHeader = LocalizationHelper.GetString("OperBoxExportHeaderLevel");
        var ownHeader = LocalizationHelper.GetString("OperBoxExportHeaderOwn");
        var potentialHeader = LocalizationHelper.GetString("OperBoxExportHeaderPotential");
        var yes = LocalizationHelper.GetString("OperBoxExportYes");
        var no = LocalizationHelper.GetString("OperBoxExportNo");

        yield return $"| {nameHeader} | {idHeader} | {rarityHeader} | {eliteHeader} | {levelHeader} | {ownHeader} | {potentialHeader} |";
        yield return "| :-- | :-- | :-- | :-- | :-- | :-- | :-- |";
        foreach (var item in items)
        {
            yield return $"| {item.Name} | {item.Id} | {item.Rarity} | {item.Elite} | {item.Level} | {(item.Own ? yes : no)} | {item.Potential} |";
        }
    }

    private static IEnumerable<string> BuildOperBoxCsvExportLines(IReadOnlyList<OperBoxData.OperData> items)
    {
        var nameHeader = LocalizationHelper.GetString("OperBoxExportHeaderName");
        var idHeader = LocalizationHelper.GetString("OperBoxExportHeaderId");
        var rarityHeader = LocalizationHelper.GetString("OperBoxExportHeaderRarity");
        var eliteHeader = LocalizationHelper.GetString("OperBoxExportHeaderElite");
        var levelHeader = LocalizationHelper.GetString("OperBoxExportHeaderLevel");
        var ownHeader = LocalizationHelper.GetString("OperBoxExportHeaderOwn");
        var potentialHeader = LocalizationHelper.GetString("OperBoxExportHeaderPotential");
        var yes = LocalizationHelper.GetString("OperBoxExportYes");
        var no = LocalizationHelper.GetString("OperBoxExportNo");

        yield return $"{nameHeader},{idHeader},{rarityHeader},{eliteHeader},{levelHeader},{ownHeader},{potentialHeader}";
        foreach (var item in items)
        {
            var name = item.Name ?? string.Empty;
            if (name.Contains(',') || name.Contains('"') || name.Contains('\n'))
            {
                name = "\"" + name.Replace("\"", "\"\"") + "\"";
            }

            yield return $"{name},{item.Id},{item.Rarity},{item.Elite},{item.Level},{(item.Own ? yes : no)},{item.Potential}";
        }
    }

    #endregion OperBox

    #region Gacha

    private string _gachaInfo = LocalizationHelper.GetString("GachaInitTip");

    public string GachaInfo
    {
        get => _gachaInfo;
        set => SetAndNotify(ref _gachaInfo, value);
    }

    // UI 缁戝畾鐨勬柟娉?
    public async Task GachaOnce()
    {
        await StartGacha();
    }

    // UI 缁戝畾鐨勬柟娉?
    public async Task GachaTenTimes()
    {
        await StartGacha(false);
    }

    private bool _isGachaInProgress;

    public bool IsGachaInProgress
    {
        get => _isGachaInProgress;
        set {
            if (!SetAndNotify(ref _isGachaInProgress, value))
            {
                return;
            }

            if (!value)
            {
                _gachaTimer.Stop();
                GachaInfo = LocalizationHelper.GetString("GachaInitTip");
            }
        }
    }

    public async Task StartGacha(bool once = true)
    {
        _runningState.SetIdle(false);

        string errMsg = string.Empty;
        GachaInfo = LocalizationHelper.GetString("ConnectingToEmulator");
        bool caught = await Task.Run(() => Instances.AsstProxy.AsstConnect(ref errMsg) && Instances.AsstProxy.AsstStartGacha(once));
        if (!caught)
        {
            GachaInfo = errMsg;
            _runningState.SetIdle(true);
            return;
        }

        _gachaTimer.Interval = TimeSpan.FromSeconds(5);
        _gachaTimer.Start();

        RefreshGachaTip(null, null);
        IsGachaInProgress = true;
        _ = Peep();
    }

    private void RefreshGachaTip(object? sender, EventArgs? e)
    {
        var rd = new Random();
        GachaInfo = LocalizationHelper.GetString("GachaTip" + rd.Next(1, 18));
    }

    // DO NOT CHANGE
    // 璇峰嬁鏇存敼
    // 璜嬪嬁鏇存敼
    // 銇撱伄銈炽兗銉夈倰澶夋洿銇椼仾銇勩仹銇忋仩銇曘亜
    // 氤€瓴巾晿歆€ 毵堨嫮鞁滌槫
    private bool _gachaShowDisclaimer = true; // !ConfigurationHelper.GetValue(ConfigurationKeys.ShowDisclaimerNoMore, false);

    public bool GachaShowDisclaimer
    {
        get => _gachaShowDisclaimer;
        set {
            SetAndNotify(ref _gachaShowDisclaimer, value);
        }
    }

    public bool GachaShowDisclaimerNoMore
    {
        get => ConfigFactory.CurrentConfig.Toolbox.GachaShowDisclaimerNoMore;
        set {
            ConfigFactory.CurrentConfig.Toolbox.GachaShowDisclaimerNoMore = value;
            NotifyOfPropertyChange();
        }
    }

    // UI 缁戝畾鐨勬柟娉?
    [UsedImplicitly]
    public void GachaAgreeDisclaimer()
    {
        var result = MessageBoxHelper.Show(
            LocalizationHelper.GetString("GachaWarning"),
            LocalizationHelper.GetString("Warning"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            no: LocalizationHelper.GetString("Confirm"),
            yes: LocalizationHelper.GetString("Cancel"),
            iconBrushKey: "DangerBrush");
        if (result != MessageBoxResult.No)
        {
            return;
        }

        AchievementTrackerHelper.Instance.Unlock(AchievementIds.RealGacha);

        GachaShowDisclaimer = false;
    }

    #endregion Gacha

    #region Peep

    private bool _peeping;

    public bool Peeping
    {
        get => _peeping;
        set {
            if (!SetAndNotify(ref _peeping, value))
            {
                return;
            }

            if (!value)
            {
                _peepImageTimer.Stop();
            }
        }
    }

    private bool _isPeepInProgress;

    /// <summary>
    /// Gets or sets a value indicating whether鐢?Peep 鏂规硶鍚姩鐨?Peep
    /// </summary>
    public bool IsPeepInProgress
    {
        get => _isPeepInProgress;
        set {
            if (!SetAndNotify(ref _isPeepInProgress, value))
            {
                return;
            }

            if (!value)
            {
                _peepImageTimer.Stop();
            }
        }
    }

    private WriteableBitmap? _peepImage;

    public WriteableBitmap? PeepImage
    {
        get => _peepImage;
        set => SetAndNotify(ref _peepImage, value);
    }

    private double _peepScreenFpf;

    public double PeepScreenFpf
    {
        get => _peepScreenFpf;
        set => SetAndNotify(ref _peepScreenFpf, value);
    }

    public int PeepTargetFps
    {
        get; set {
            value = value switch {
                > 600 => 600,
                < 1 => 1,
                _ => value,
            };

            SetAndNotify(ref field, value);
            _peepImageTimer.Interval = 1000d / field;
            ConfigFactory.CurrentConfig.Toolbox.PeepTargetFps = value;
        }
    } = ConfigFactory.CurrentConfig.Toolbox.PeepTargetFps;

    private DateTime _lastFpsUpdateTime = DateTime.MinValue;
    private int _frameCount;

    private readonly Timer _peepImageTimer = new();
    private readonly DispatcherTimer _gachaTimer = new() { Interval = TimeSpan.FromSeconds(5) };

    private int _peepImageCount;
    private int _peepImageNewestCount;

    private static int _peepImageSemaphoreCurrentCount = 2;
    private const int PeepImageSemaphoreMaxCount = 5;
    private static int _peepImageSemaphoreFailCount = 0;
    private static readonly SemaphoreSlim _peepImageSemaphore = new(_peepImageSemaphoreCurrentCount, PeepImageSemaphoreMaxCount);

    private async void PeepImageTimerElapsed(object? sender, EventArgs? e)
    {
        try
        {
            await RefreshPeepImageAsync();
        }
        catch
        {
            // ignored
        }
    }

    private readonly WriteableBitmap?[] _peepImageCache = new WriteableBitmap?[PeepImageSemaphoreMaxCount];

    private async Task RefreshPeepImageAsync()
    {
        if (!await _peepImageSemaphore.WaitAsync(0))
        {
            // 涓€绉掑唴杩炵画涓夋鏈兘鑾峰彇淇″彿閲忥紝闄嶄綆 FPS
            if (++_peepImageSemaphoreFailCount < 3)
            {
                return;
            }

            _peepImageSemaphoreFailCount = 0;

            if (_peepImageSemaphoreCurrentCount < PeepImageSemaphoreMaxCount)
            {
                _peepImageSemaphoreCurrentCount++;
                _peepImageSemaphore.Release();
                _logger.Information("Screenshot Semaphore Full, increase semaphore count to {PeepImageSemaphoreCurrentCount}", _peepImageSemaphoreCurrentCount);
                return;
            }

            _logger.Warning("Screenshot Semaphore Full, Reduce Target FPS count to {PeepTargetFps}", --PeepTargetFps);
            _ = Execute.OnUIThreadAsync(() => {
                Growl.Clear();
                Growl.Warning($"Screenshot taking too long, reduce Target FPS to {PeepTargetFps}");
            });
            return;
        }

        try
        {
            var count = Interlocked.Increment(ref _peepImageCount);
            var index = count % _peepImageCache.Length;
            var frameData = await Instances.AsstProxy.AsstGetImageBgrDataAsync(forceScreencap: true);
            if (frameData is null || frameData.Length == 0)
            {
                _logger.Warning("Peep image data is null or empty.");
                return;
            }

            // 鑻ヤ笉婊¤冻鏉′欢锛屾彁鍓嶉噴鏀?frameData 閬垮厤鍐呭瓨娉勯湶
            if (!Peeping || count <= _peepImageNewestCount)
            {
                _logger.Debug("Peep image count {Count} is not the newest, skip updating image.", count);
                ArrayPool<byte>.Shared.Return(frameData);
                return;
            }

            await Execute.OnUIThreadAsync(() => {
                _peepImageCache[index] = AsstProxy.WriteBgrToBitmap(frameData, _peepImageCache[index]);
            });

            PeepImage = _peepImageCache[index];
            ArrayPool<byte>.Shared.Return(frameData);
            Interlocked.Exchange(ref _peepImageNewestCount, count);

            var now = DateTime.Now;
            Interlocked.Increment(ref _frameCount);
            var totalSeconds = (now - _lastFpsUpdateTime).TotalSeconds;
            if (totalSeconds < 1)
            {
                return;
            }

            var frameCount = Interlocked.Exchange(ref _frameCount, 0);
            _lastFpsUpdateTime = now;
            PeepScreenFpf = frameCount / totalSeconds;
            _peepImageSemaphoreFailCount = 0;
        }
        finally
        {
            _peepImageSemaphore.Release();
        }
    }

    private bool _isPeepTransitioning;

    public bool IsPeepTransitioning
    {
        get => _isPeepTransitioning;
        set => SetAndNotify(ref _isPeepTransitioning, value);
    }

    /// <summary>
    /// 鑾峰彇鎴栧仠姝㈣幏鍙栧疄鏃舵埅鍥撅紝鍦ㄦ娊鍗℃椂棰濆鍋滄鎶藉崱
    /// </summary>
    /// <returns>Task</returns>
    public async Task Peep()
    {
        if (IsPeepTransitioning)
        {
            return;
        }

        IsPeepTransitioning = true;

        try
        {
            // 姝ｅ湪 Peep 鏃讹紝鐐瑰嚮鎸夐挳鍋滄 Peep
            if (Peeping)
            {
                Peeping = false;
                _peepImageTimer.Stop();
                Array.Fill(_peepImageCache, null);

                // 鐢?Peep() 鏂规硶鍚姩鐨?Peep 涔熼渶瑕佸仠姝紝Block 涓嶄細鑷姩鍋滄
                if (IsGachaInProgress || IsPeepInProgress)
                {
                    await Instances.TaskQueueViewModel.Stop();
                    Instances.TaskQueueViewModel.SetStopped();
                }

                IsPeepInProgress = false;
                IsGachaInProgress = false;
                return;
            }

            // 鐐瑰嚮鎸夐挳寮€濮?Peep
            Peeping = true;

            AchievementTrackerHelper.Instance.Unlock(AchievementIds.PeekScreen);

            // 濡傛灉娌′换鍔″湪杩愯锛岄渶瑕佸厛杩炴帴锛屽苟鏍囪鏄敱 Peep() 鏂规硶鍚姩鐨?Peep
            if (Idle)
            {
                _runningState.SetIdle(false);
                string errMsg = string.Empty;
                bool caught = await Task.Run(() => Instances.AsstProxy.AsstConnect(ref errMsg));
                if (!caught)
                {
                    GachaInfo = errMsg;
                    _runningState.SetIdle(true);
                    return;
                }

                IsPeepInProgress = true;
            }

            PeepScreenFpf = 0;
            _peepImageCount = 0;
            _peepImageNewestCount = 0;
            _peepImageTimer.Start();
        }
        finally
        {
            IsPeepTransitioning = false;
        }
    }

    #endregion

    #region MiniGame

    public class MiniGameCategoryItem : PropertyChangedBase
    {
        public string Display { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public bool IsSecretFront => Value == "MiniGame@SecretFront";

        public bool IsPixelPaint => Value is "MiniGame@PixelPaint" or "MiniGame@PixelPaint@Begin";
    }

    public ObservableCollection<MiniGameCategoryItem> MiniGameCategoryItems { get; } = [];

    private MiniGameCategoryItem? _selectedMiniGameItem;

    public MiniGameCategoryItem? SelectedMiniGameItem
    {
        get => _selectedMiniGameItem;
        set {
            if (!SetAndNotify(ref _selectedMiniGameItem, value) || value == null)
            {
                return;
            }

            MiniGameTaskName = value.Value;
        }
    }

    /// <summary>
    /// Gets the index of the selected mini game in the list.
    /// </summary>
    // 娉細MiniGameCategoryItems 鍦?UpdateMiniGameTaskList 涓細 Clear+閲嶅缓锛岃嫢姝ゅ埢鏈€変腑椤瑰凡涓嶅湪鍒楄〃锛?
    // IndexOf 杩斿洖 -1锛堥殢鍚庣敱 setter 閲嶆柊瀵归綈锛夛紱
    [PropertyDependsOn(nameof(SelectedMiniGameItem))]
    public int SelectedMiniGameIndex => SelectedMiniGameItem is { } selected
        ? MiniGameCategoryItems.IndexOf(selected)
        : -1;

    public bool IsPixelPaintSelected => SelectedMiniGameItem?.IsPixelPaint == true;

    public static void UpdateMiniGameTaskList()
    {
        var categorizedItems = Instances.StageManager.MiniGameEntries
            .Select(t => {
                var isCurrentEvent = t.UtcStartTime != DateTime.MinValue || t.UtcExpireTime != DateTime.MinValue;
                var category = LocalizationHelper.GetString(isCurrentEvent
                    ? "MiniGameCategoryCurrentEvent"
                    : "MiniGameCategoryPermanent");
                return new MiniGameCategoryItem {
                    Display = string.IsNullOrEmpty(t.DisplayKey)
                        ? t.Display
                        : (LocalizationHelper.TryGetString(t.DisplayKey, out var loc) ? loc : t.Display),
                    Value = t.Value,
                    Category = category,
                };
            })
            .ToList();

        Execute.OnUIThread(() => {
            var toolbox = Instances.ToolboxViewModel;
            if (toolbox == null)
            {
                return;
            }

            var prevSelected = toolbox.SelectedMiniGameItem?.Value;

            toolbox.MiniGameCategoryItems.Clear();
            foreach (var item in categorizedItems)
            {
                toolbox.MiniGameCategoryItems.Add(item);
            }

            toolbox.SelectedMiniGameItem = toolbox.MiniGameCategoryItems
                .FirstOrDefault(i => i.Value == prevSelected)
                ?? toolbox.MiniGameCategoryItems.FirstOrDefault();
        });
    }

    public string MiniGameTaskName
    {
        get; set {
            SetAndNotify(ref field, value);
            MiniGameTip = GetMiniGameTip(value);
        }
    } = "SS@Store@Begin";

    public string GetMiniGameTask()
    {
        return MiniGameTaskName switch {
            "MiniGame@SecretFront" => $"{MiniGameTaskName}@Begin@Ending{SecretFrontEnding}{(string.IsNullOrEmpty(SecretFrontEvent) ? string.Empty : $"@{SecretFrontEvent}")}",
            _ => MiniGameTaskName,
        };
    }

    private string? _miniGameTip;

    public string MiniGameTip
    {
        get {
            _miniGameTip ??= GetMiniGameTip(MiniGameTaskName);
            return _miniGameTip;
        }
        set => SetAndNotify(ref _miniGameTip, value);
    }

    private static string GetMiniGameTip(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return LocalizationHelper.GetString("MiniGameNameEmptyTip");
        }

        var entry = Instances.StageManager.MiniGameEntries.FirstOrDefault(e => e.Value == name);
        if (entry == null)
        {
            return LocalizationHelper.GetString("MiniGameNameEmptyTip");
        }

        // 浼樺厛浣跨敤 TipKey 鐨勬湰鍦板寲
        if (!string.IsNullOrEmpty(entry.TipKey) && LocalizationHelper.TryGetString(entry.TipKey, out var tipFromKey))
        {
            return tipFromKey;
        }

        // 鐒跺悗浣跨敤 API Tip
        if (!string.IsNullOrEmpty(entry.Tip))
        {
            return entry.Tip;
        }

        // 鑻ヤ笉瀛樺湪 Tip锛屽啀灏濊瘯浣跨敤 DisplayKey + "Tip" 鐨勭害瀹氶敭
        if (!string.IsNullOrEmpty(entry.DisplayKey))
        {
            var displayTipKey = entry.DisplayKey + "Tip";
            if (LocalizationHelper.TryGetString(displayTipKey, out var displayTip))
            {
                return displayTip;
            }

            // 鏈€鍚庡洖閫€涓?Display 鐨勬湰鍦板寲鎴栧師濮?Display
            if (LocalizationHelper.TryGetString(entry.DisplayKey, out var displayLoc))
            {
                return displayLoc;
            }
        }

        if (!string.IsNullOrEmpty(entry.Display))
        {
            return entry.Display;
        }

        return string.Empty;
    }

    public List<string> SecretFrontEndingList { get; set; } = ["A", "B", "C", "D", "E"];

    public string SecretFrontEnding { get; set => SetAndNotify(ref field, value); } = "A";

    public LocalizedObservableList<string> SecretFrontEventList { get; } = new(
        (string.Empty, "NotSelected"),
        ("鏀彺浣滄垬骞冲彴", "MiniGame@SecretFront@Event1"),
        ("娓镐緺", "MiniGame@SecretFront@Event2"),
        ("璇″奖杩疯釜", "MiniGame@SecretFront@Event3"));

    public string SecretFrontEvent { get; set => SetAndNotify(ref field, value); } = string.Empty;

    #region PixelPaint

    /// <summary>鍍忕礌鐢绘敮鎸佺殑鍥剧墖鎵╁睍鍚嶏紝鏂囦欢瀵硅瘽妗嗚繃婊ゅ櫒涓庡壀璐存澘鏂囦欢鍒ゆ柇鍏辩敤銆?/summary>
    private static readonly string[] _pixelPaintImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif"];

    private BitmapSource? _pixelPaintSourceImage;

    private BitmapSource? _pixelPaintPreview;

    public BitmapSource? PixelPaintPreview
    {
        get => _pixelPaintPreview;
        private set => SetAndNotify(ref _pixelPaintPreview, value);
    }

    private string _pixelPaintStatusText = string.Empty;

    public string PixelPaintStatusText
    {
        get => _pixelPaintStatusText;
        private set => SetAndNotify(ref _pixelPaintStatusText, value);
    }

    private PixelPaintHelper.PreparedImage? _pixelPaintPrepared;

    private PixelPaintHelper.ConvertResult? _pixelPaintResult;

    private bool _pixelPaintParametersLocked;

    public bool PixelPaintParametersLocked
    {
        get => _pixelPaintParametersLocked;
        private set => SetAndNotify(ref _pixelPaintParametersLocked, value);
    }

    /// <summary>鐩稿鍘昏竟鍚庡唴瀹瑰浘鐨勫綊涓€鍖栧彇鏅紙0~1锛夈€?/summary>
    private System.Windows.Rect _pixelPaintView = new(0, 0, 1, 1);

    private System.Windows.Point? _pixelPaintDragStart;

    private System.Windows.Rect _pixelPaintDragOriginView;

    public LocalizedObservableList<string> PixelPaintFitModeList { get; } = new(
        ("Crop", "MiniGame@PixelPaint@FitCrop"),
        ("Contain", "MiniGame@PixelPaint@FitContain"),
        ("Stretch", "MiniGame@PixelPaint@FitStretch"));

    public string PixelPaintFitMode
    {
        get; set {
            if (SetAndNotify(ref field, value))
            {
                ReconvertPixelPaint();
            }
        }
    } = "Crop";

    public LocalizedObservableList<string> PixelPaintDitherModeList { get; } = new(
        ("Illustration", "MiniGame@PixelPaint@DitherIllustration"),
        ("None", "MiniGame@PixelPaint@DitherNone"),
        ("FloydSteinberg", "MiniGame@PixelPaint@DitherFS"),
        ("Atkinson", "MiniGame@PixelPaint@DitherAtkinson"));

    public string PixelPaintDitherMode
    {
        get; set {
            if (SetAndNotify(ref field, value))
            {
                ReconvertPixelPaint();
            }
        }
    } = "Illustration";

    public double PixelPaintContrast
    {
        get; set {
            if (SetAndNotify(ref field, value))
            {
                ReconvertPixelPaint();
            }
        }
    } = 100;

    public double PixelPaintBrightness
    {
        get; set {
            if (SetAndNotify(ref field, value))
            {
                ReconvertPixelPaint();
            }
        }
    } = 100;

    public double PixelPaintSaturation
    {
        get; set {
            if (SetAndNotify(ref field, value))
            {
                ReconvertPixelPaint();
            }
        }
    } = 100;

    public bool PixelPaintPaintWhite
    {
        get; set {
            if (SetAndNotify(ref field, value))
            {
                ReconvertPixelPaint();
            }
        }
    }

    /// <summary>鎷栧姩缁樺埗寮€鍏筹細鍚岃壊鍚岃杩炵画鏍间竴娆＄敾瀹岋紙鏇村揩锛岄儴鍒嗚Е鎺фā寮忓彲鑳戒涪鐐癸級銆?/summary>
    public bool PixelPaintSwipeEnabled { get; set; } = true;

    /// <summary>姣忔牸棰濆绛夊緟锛坢s锛夛紝榛樿 0锛涚偣鍑诲悗绛夊緟涓庢嫋鍔ㄦ椂闀垮潎浼氱疮鍔狅紙鍚勮Е鎺ф柟寮忚嚜甯﹀熀纭€闂撮殧锛夈€?/summary>
    public int PixelPaintGridDelay { get; set; } = 0;

    public void PixelPaintPickImage()
    {
        if (PixelPaintParametersLocked)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog {
            Filter = "Image|" + string.Join(";", _pixelPaintImageExtensions.Select(ext => "*" + ext)) + "|All|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        LoadPixelPaintImage(dialog.FileName);
    }

    public void PixelPaintDrop(object sender, DragEventArgs e)
    {
        if (PixelPaintParametersLocked || e.Data == null)
        {
            return;
        }

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        LoadPixelPaintImage(files[0]);
    }

    public void PixelPaintDragOver(object sender, DragEventArgs e)
    {
        e.Effects = (!PixelPaintParametersLocked && e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// 鍍忕礌鐢伙細鍝嶅簲 Ctrl+V锛屼粠鍓创鏉跨矘璐淬€?
    /// 浼樺厛鍓创鏉夸綅鍥撅紙鎴浘銆佹祻瑙堝櫒 锝㈠鍒跺浘鐗囷剑 绛夋棤鏂囦欢鍦烘櫙锛夛紝
    /// 鍏舵宸插鍒剁殑鍥剧墖鏂囦欢锛屾渶鍚庡壀璐存澘鏂囧瓧鈥斺€旀枃瀛椾細娓叉煋鎴愬洓瑙掑竷灞€鍥剧墖锛?
    /// 鍔犺浇澶辫触鏃朵笌鏂囦欢鍔犺浇涓€鑷村湴鎻愮ず銆?
    /// </summary>
    /// <param name="sender">浜嬩欢婧愶紙缁戝畾 KeyDown 鐨?MiniGame Grid锛夈€?/param>
    /// <param name="e">鎸夐敭浜嬩欢鏁版嵁銆?/param>
    public void PixelPaintKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control || !IsPixelPaintSelected || PixelPaintParametersLocked)
        {
            return;
        }

        try
        {
            if (Clipboard.ContainsImage() || Clipboard.ContainsData(DataFormats.Dib))
            {
                var bmp = GetClipboardImage();
                if (bmp != null)
                {
                    LoadPixelPaintImage(bmp);
                }
            }
            else if (Clipboard.GetFileDropList() is { Count: > 0 } files && files[0] is { } file)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (_pixelPaintImageExtensions.Contains(ext))
                {
                    LoadPixelPaintImage(file);
                }
            }
            else if (Clipboard.ContainsText())
            {
                // 澶嶅埗鐨勬槸鏂囧瓧锛氭覆鏌撴垚鍥涜甯冨眬鍥剧墖锛堝彇鍓?4 涓瓧绱狅級
                var text = Clipboard.GetText().Trim();
                if (text.Length > 0)
                {
                    LoadPixelPaintImage(PixelPaintHelper.RenderTextToBitmap(text));
                }
            }
        }
        catch (Exception ex)
        {
            // 鍓创鏉夸綅鍥惧姞杞藉紓甯革紙濡傛牸寮忎笉琚?Prepare 鏀寔锛夛紝涓庢枃浠跺姞杞戒竴鑷村湴鎻愮ず
            _logger.Warning(ex, "Paste pixel paint image failed");
            PixelPaintStatusText = LocalizationHelper.GetString("MiniGame@PixelPaint@LoadFailed");
        }
    }

    /// <summary>
    /// 浠庡壀璐存澘鍙栧浘銆備紭鍏堢敤 DIB 鍘熷鏁版嵁鑷瑙ｇ爜鈥斺€?
    /// WPF 鐨?Clipboard.GetImage() 瀵?DIB 鐨勮浆鎹㈠瓨鍦ㄩ€氶亾閿欎贡/灏哄閿欎贡闂锛?
    /// 閲嶅缓 BMP 鏂囦欢澶磋嚜琛岃В鐮佸彲瑙勯伩璇ョ己闄凤紱澶辫触鍒欏洖閫€鍒?GetImage()銆?
    /// </summary>
    /// <returns>瑙ｇ爜鍚庣殑 BitmapSource锛涘壀璐存澘鏃犲浘鎴栬В鐮佸け璐ユ椂杩斿洖 null銆?/returns>
    private static BitmapSource? GetClipboardImage()
    {
        try
        {
            if (Clipboard.ContainsData(DataFormats.Dib) && Clipboard.GetData(DataFormats.Dib) is Stream dib)
            {
                return DecodeDibAsBmp(dib);
            }
        }
        catch
        {
            // ignored
        }

        try
        {
            return Clipboard.GetImage();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 灏嗗壀璐存澘 DIB锛圔ITMAPINFOHEADER + 璋冭壊鏉?+ 鍍忕礌鏁版嵁锛夎В鐮佷负 BitmapSource锛?
    /// 鍦ㄥ墠闈㈣ˉ涓€涓?14 瀛楄妭鐨?BITMAPFILEHEADER锛屾嫾鎴愬畬鏁?BMP 鍚庝氦缁欒В鐮佸櫒銆?
    /// </summary>
    /// <param name="dib">鍓创鏉?DIB 鏁版嵁娴侊紙BITMAPINFOHEADER + 璋冭壊鏉?+ 鍍忕礌锛夈€?/param>
    /// <returns>瑙ｇ爜鍚庣殑 BitmapSource锛涙暟鎹潪娉曟垨杩囩煭鏃惰繑鍥?null銆?/returns>
    private static BitmapSource? DecodeDibAsBmp(Stream dib)
    {
        if (dib.Length < 40)
        {
            return null;
        }

        // 璇诲彇 BITMAPINFOHEADER 鍥哄畾鍓?40 瀛楄妭锛岃绠楀儚绱犳暟鎹湪鏂囦欢涓殑鍋忕Щ
        var info = new byte[40];
        dib.Position = 0;
        dib.ReadExactly(info, 0, info.Length);
        var biSize = BitConverter.ToInt32(info, 0);
        var biBitCount = BitConverter.ToInt16(info, 14);
        var biClrUsed = BitConverter.ToInt32(info, 32);
        var paletteSize = biBitCount <= 8
            ? (biClrUsed > 0 ? biClrUsed : 1 << biBitCount) * 4
            : 0;
        var offset = 14 + biSize + paletteSize;
        if (biSize < 40 || offset > dib.Length)
        {
            return null;
        }

        // 鏋勯€?BMP 鏂囦欢澶达細'BM' 鏍囧織銆佹枃浠舵€婚暱搴︺€佸儚绱犳暟鎹亸绉?
        var header = new byte[14];
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        BitConverter.GetBytes((int)dib.Length + 14).CopyTo(header, 2);
        BitConverter.GetBytes(offset).CopyTo(header, 10);

        var merged = new MemoryStream();
        merged.Write(header, 0, header.Length);
        dib.Position = 0;
        dib.CopyTo(merged);
        merged.Position = 0;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = merged;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    public void PixelPaintPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (PixelPaintParametersLocked || _pixelPaintSourceImage == null)
        {
            return;
        }

        // 婊氳疆缂╂斁鍙栨櫙锛氬悜涓婃斁澶э紙缂╁皬 view锛夛紝鍚戜笅缂╁皬锛堟斁澶?view锛?
        var factor = e.Delta > 0 ? 0.9 : 1.0 / 0.9;
        var cx = _pixelPaintView.X + (_pixelPaintView.Width / 2);
        var cy = _pixelPaintView.Y + (_pixelPaintView.Height / 2);
        var nw = Math.Clamp(_pixelPaintView.Width * factor, 0.05, 1.0);
        var nh = Math.Clamp(_pixelPaintView.Height * factor, 0.05, 1.0);
        var nx = Math.Clamp(cx - (nw / 2), 0, 1 - nw);
        var ny = Math.Clamp(cy - (nh / 2), 0, 1 - nh);
        _pixelPaintView = new System.Windows.Rect(nx, ny, nw, nh);
        ReconvertPixelPaint();
        e.Handled = true;
    }

    public void PixelPaintPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (PixelPaintParametersLocked || _pixelPaintSourceImage == null)
        {
            return;
        }

        if (sender is not IInputElement el)
        {
            return;
        }

        _pixelPaintDragStart = e.GetPosition(el);
        _pixelPaintDragOriginView = _pixelPaintView;
        el.CaptureMouse();
        e.Handled = true;
    }

    public void PixelPaintPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pixelPaintDragStart is null || PixelPaintParametersLocked)
        {
            return;
        }

        if (sender is not FrameworkElement el)
        {
            return;
        }

        var pos = e.GetPosition(el);
        var dx = (pos.X - _pixelPaintDragStart.Value.X) / Math.Max(1.0, el.ActualWidth);
        var dy = (pos.Y - _pixelPaintDragStart.Value.Y) / Math.Max(1.0, el.ActualHeight);

        // 鎷栧浘鍍忥細榧犳爣鍙崇Щ鏃跺唴瀹瑰乏绉伙紙view.X 鍑忓皬锛?
        var nx = Math.Clamp(_pixelPaintDragOriginView.X - (dx * _pixelPaintDragOriginView.Width), 0, 1 - _pixelPaintDragOriginView.Width);
        var ny = Math.Clamp(_pixelPaintDragOriginView.Y - (dy * _pixelPaintDragOriginView.Height), 0, 1 - _pixelPaintDragOriginView.Height);
        _pixelPaintView = new System.Windows.Rect(nx, ny, _pixelPaintDragOriginView.Width, _pixelPaintDragOriginView.Height);
        ReconvertPixelPaint();
        e.Handled = true;
    }

    public void PixelPaintPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputElement el && el.IsMouseCaptured)
        {
            el.ReleaseMouseCapture();
        }

        _pixelPaintDragStart = null;
        e.Handled = true;
    }

    public void PixelPaintResetView()
    {
        if (PixelPaintParametersLocked)
        {
            return;
        }

        _pixelPaintView = new System.Windows.Rect(0, 0, 1, 1);
        ReconvertPixelPaint();
    }

    public void PixelPaintResetParameters()
    {
        if (PixelPaintParametersLocked)
        {
            return;
        }

        _pixelPaintView = new System.Windows.Rect(0, 0, 1, 1);
        PixelPaintFitMode = "Crop";
        PixelPaintDitherMode = "Illustration";
        PixelPaintContrast = 100;
        PixelPaintBrightness = 100;
        PixelPaintSaturation = 100;
        PixelPaintPaintWhite = false;
        ReconvertPixelPaint();
    }

    private void LoadPixelPaintImage(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();

            LoadPixelPaintImage(bmp);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Load pixel paint image failed: {Path}", path);
            PixelPaintStatusText = LocalizationHelper.GetString("MiniGame@PixelPaint@LoadFailed");
        }
    }

    private void LoadPixelPaintImage(BitmapSource bmp)
    {
        if (bmp.CanFreeze)
        {
            bmp.Freeze();
        }

        _pixelPaintSourceImage = bmp;
        _pixelPaintPrepared = PixelPaintHelper.Prepare(bmp, trimEmptyBorder: true);
        _pixelPaintView = new System.Windows.Rect(0, 0, 1, 1);
        ReconvertPixelPaint();
    }

    private void ReconvertPixelPaint()
    {
        if (PixelPaintParametersLocked || _pixelPaintSourceImage == null)
        {
            return;
        }

        try
        {
            var fit = PixelPaintFitMode switch {
                "Contain" => PixelPaintHelper.FitMode.Contain,
                "Stretch" => PixelPaintHelper.FitMode.Stretch,
                _ => PixelPaintHelper.FitMode.Crop,
            };
            var dither = PixelPaintDitherMode switch {
                "None" => PixelPaintHelper.DitherMode.None,
                "Atkinson" => PixelPaintHelper.DitherMode.Atkinson,
                "Illustration" => PixelPaintHelper.DitherMode.Illustration,
                _ => PixelPaintHelper.DitherMode.FloydSteinberg,
            };

            var options = new PixelPaintHelper.ConvertOptions {
                Fit = fit,
                Dither = dither,
                ContrastPercent = PixelPaintContrast,
                BrightnessPercent = PixelPaintBrightness,
                SaturationPercent = PixelPaintSaturation,
                ContentViewNormalized = _pixelPaintView,
                TrimEmptyBorder = true,
            };

            if (_pixelPaintPrepared == null)
            {
                return;
            }

            var result = PixelPaintHelper.Convert(_pixelPaintPrepared, options, skipWhite: !PixelPaintPaintWhite);
            _pixelPaintResult = result;
            PixelPaintPreview = result.Preview;
            PixelPaintStatusText = string.Format(
                LocalizationHelper.GetString("MiniGame@PixelPaint@ReadyStatus"),
                result.PaintedCellCount,
                result.Groups.Count);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Pixel paint convert failed");
            _pixelPaintResult = null;
            PixelPaintPreview = null;
            PixelPaintStatusText = LocalizationHelper.GetString("MiniGame@PixelPaint@ConvertFailed");
        }
    }

    #endregion

    public void StartMiniGame()
    {
        _ = StartMiniGameAsync();
    }

    private async Task StartMiniGameAsync()
    {
        if (!Idle)
        {
            await Instances.TaskQueueViewModel.Stop();
            return;
        }

        var isPixelPaint = IsPixelPaintSelected;
        if (isPixelPaint && (_pixelPaintResult == null || _pixelPaintResult.Groups.Count == 0))
        {
            Instances.TaskQueueViewModel.AddLog(LocalizationHelper.GetString("MiniGame@PixelPaint@NeedImage"), UiLogColor.Warning);
            return;
        }

        Instances.TaskQueueViewModel.ClearLog();

        _runningState.SetIdle(false);
        if (isPixelPaint)
        {
            PixelPaintParametersLocked = true;
        }

        string errMsg = string.Empty;
        bool caught = await Task.Run(() => Instances.AsstProxy.AsstConnect(ref errMsg));
        if (!caught)
        {
            Instances.TaskQueueViewModel.AddLog(errMsg, UiLogColor.Error);
            _runningState.SetIdle(true);
            PixelPaintParametersLocked = false;
            return;
        }

        if (_runningState.GetStopping())
        {
            Instances.TaskQueueViewModel.SetStopped();
            PixelPaintParametersLocked = false;
            return;
        }

        if (isPixelPaint)
        {
            var groups = _pixelPaintResult!.Groups;
            caught = Instances.AsstProxy.AsstPixelPaint(groups, PixelPaintSwipeEnabled, PixelPaintGridDelay);
            if (caught)
            {
                Instances.TaskQueueViewModel.AddLog(
                    string.Format(
                        LocalizationHelper.GetString("MiniGame@PixelPaint@StartLog"),
                        groups.Sum(g => g.Points.Count),
                        groups.Count),
                    UiLogColor.Info);
            }
        }
        else
        {
            caught = Instances.AsstProxy.AsstMiniGame(GetMiniGameTask());
        }

        if (!caught)
        {
            _runningState.SetIdle(true);
            PixelPaintParametersLocked = false;
        }
        else
        {
            AchievementTrackerHelper.Instance.Unlock(AchievementIds.SlackingOff);
        }
    }

    #endregion
}
