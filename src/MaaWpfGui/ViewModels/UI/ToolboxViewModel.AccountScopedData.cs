// <copyright file="ToolboxViewModel.AccountScopedData.cs" company="MaaAssistantArknights">
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
using System.Text;
using HandyControl.Controls;
using MaaWpfGui.Configuration.Factory;
using MaaWpfGui.Configuration.Single.MaaTask;
using MaaWpfGui.Constants;
using MaaWpfGui.Helper;
using MaaWpfGui.Utilities.ValueType;
using MaaWpfGui.ViewModels.UserControl.Settings;
using Newtonsoft.Json.Linq;
using Stylet;

namespace MaaWpfGui.ViewModels.UI;

/// <summary>
/// feat/account-scoped-recognition-data partial class.
/// 把干员/仓库识别数据按账号分桶存储的全部逻辑 (<see cref="ToolboxViewModel.SaveDepotDetails"/> 等) 从主文件下沉,
/// 主文件 <see cref="ToolboxViewModel"/> 仅保留 ~6 个一行级 partial 方法调用。
/// 减少与 upstream/master-v2 合并时主文件的冲突面 (下游 [HOT] 详见 docs/downstream-changes.md)。
/// </summary>
public partial class ToolboxViewModel
{
    #region AccountScopedRecognitionData (feat/account-scoped-recognition-data)

    private string _currentDataAccountKey = JsonDataKey.DefaultDataAccount;
    private string? _currentDataAccountRaw;
    private readonly HashSet<string> _knownDataAccountKeys = new(StringComparer.OrdinalIgnoreCase);

    private ObservableCollection<GenericCombinedData<string>> _dataAccountList = [];

    /// <summary>
    /// Gets 账号数据查看下拉的选项列表 (Value = 桶 key, Display = 账号显示名)。
    /// </summary>
    public ObservableCollection<GenericCombinedData<string>> DataAccountList
    {
        get => _dataAccountList;
        private set => SetAndNotify(ref _dataAccountList, value);
    }

    /// <summary>
    /// Gets or sets 当前查看的账号数据桶 (UI 下拉绑定, 切换即重载对应账号数据)。
    /// </summary>
    public string SelectedDataAccount
    {
        get => _currentDataAccountKey;
        set => SwitchDataAccount(value);
    }

    private GenericCombinedData<string>? _currentDataAccountOption;

    /// <summary>
    /// Gets or sets 当前账号选项 (ComboBox.SelectedItem 双向绑定, 集合替换后通过重新解析保证选中项可见)。
    /// </summary>
    public GenericCombinedData<string>? SelectedDataAccountOption
    {
        get => _currentDataAccountOption;
        set
        {
            if (value is null || ReferenceEquals(value, _currentDataAccountOption))
            {
                return;
            }

            SwitchDataAccount(value.Value);
        }
    }

    /// <summary>
    /// Gets 当前查看账号的显示名 (用于在 ComboBox 旁明示当前选中, 不依赖 ComboBox 渲染状态)。
    /// </summary>
    public string CurrentDataAccountDisplayName
    {
        get
        {
            if (_currentDataAccountKey == JsonDataKey.DefaultDataAccount)
            {
                return LocalizationHelper.GetString("DataAccountDefault");
            }

            return _currentDataAccountRaw ?? GetDataAccountDisplayName(_currentDataAccountKey);
        }
    }

    private string OperBoxBucketKey => AccountDataBucketKey(JsonDataKey.OperBoxData, _currentDataAccountKey);

    private string DepotBucketKey => AccountDataBucketKey(JsonDataKey.DepotData, _currentDataAccountKey);

    private static string AccountDataBucketKey(string baseKey, string accountKey) => $"{baseKey}_{accountKey}";

    /// <summary>
    /// 账号名 → 安全文件名。非法字符/控制字符替换为 '_'，超长截断，空值回落 _default。
    /// </summary>
    private static string SanitizeAccountKey(string? account)
    {
        var trimmed = account?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return JsonDataKey.DefaultDataAccount;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            sb.Append(char.IsControl(ch) || invalid.Contains(ch) ? '_' : ch);
        }

        var result = sb.ToString().Trim();
        if (string.IsNullOrEmpty(result))
        {
            return JsonDataKey.DefaultDataAccount;
        }

        return result.Length > 48 ? result[..48] : result;
    }

    private static string? ResolveConfiguredAccountName()
    {
        try
        {
            return ConfigFactory.CurrentConfig?.TaskQueue?.OfType<StartUpTask>().FirstOrDefault()?.AccountName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 启动时初始化: 迁移旧全局单份数据文件 → 当前配置账号桶, 并锚定初始桶。
    /// </summary>
    public void InitializeAccountScopedData()
    {
        var raw = ResolveConfiguredAccountName();
        var key = SanitizeAccountKey(raw);
        MigrateLegacyRecognitionData(key, raw);
        _currentDataAccountKey = key;
        _currentDataAccountRaw = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        _knownDataAccountKeys.Add(key);
    }

    /// <summary>
    /// 一次性迁移: data\OperBoxData.json / DepotData.json → data\OperBoxData_&lt;account&gt;.json, 旧文件改名 .bak 保留。
    /// </summary>
    private static void MigrateLegacyRecognitionData(string accountKey, string? rawAccountName)
    {
        try
        {
            foreach (var baseKey in new[] { JsonDataKey.OperBoxData, JsonDataKey.DepotData })
            {
                var legacyPath = Path.Combine(PathsHelper.DataDir, $"{baseKey}.json");
                if (!File.Exists(legacyPath))
                {
                    continue;
                }

                var bucketKey = AccountDataBucketKey(baseKey, accountKey);
                if (!JsonDataHelper.Exists(bucketKey))
                {
                    var content = JObject.Parse(File.ReadAllText(legacyPath));
                    if (!string.IsNullOrEmpty(rawAccountName) && content.ContainsKey("account") == false)
                    {
                        content["account"] = rawAccountName;
                    }

                    JsonDataHelper.Set(bucketKey, content);
                    _logger.Information("[DataAccount] migrated legacy {BaseKey} to bucket {Bucket}", baseKey, bucketKey);
                }

                File.Move(legacyPath, legacyPath + ".bak", true);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[DataAccount] legacy recognition data migration failed");
        }
    }

    /// <summary>
    /// 切换识别数据查看/写入的账号桶: 清内存 → 加载该账号桶数据。同桶时为 no-op。
    /// </summary>
    public void SwitchDataAccount(string? account, bool force = false)
    {
        var accountKey = SanitizeAccountKey(account);
        if (!force && accountKey == _currentDataAccountKey)
        {
            return;
        }

        _currentDataAccountKey = accountKey;
        _currentDataAccountRaw = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
        ClearOperBoxRecognitionData();
        DepotResult.Clear();
        ResetDepotRecognitionState();
        LoadDepotDetails();
        LoadOperBoxDetails();
        OperBoxSelectedIndex = OperBoxNotHaveList.Count > 0 ? 0 : 1;
        InvalidateDepotCache();
        Instances.TaskQueueViewModel?.UpdateDatePrompt();
        RefreshDataAccountList();
        NotifyOfPropertyChange(nameof(SelectedDataAccount));
        NotifyOfPropertyChange(nameof(CurrentDataAccountDisplayName));
        _logger.Information("[DataAccount] switched recognition data bucket to {Account}", accountKey);
    }

    /// <summary>
    /// 刷新账号下拉列表: 扫描 data 目录已有桶 + 轮换账号配置中尚无桶的账号 + 当前桶。
    /// </summary>
    public void RefreshDataAccountList()
    {
        try
        {
            var keys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                JsonDataKey.DefaultDataAccount,
                _currentDataAccountKey,
            };

            if (Directory.Exists(PathsHelper.DataDir))
            {
                foreach (var baseKey in new[] { JsonDataKey.OperBoxData, JsonDataKey.DepotData })
                {
                    var prefix = $"{baseKey}_";
                    foreach (var file in Directory.EnumerateFiles(PathsHelper.DataDir, $"{prefix}*.json"))
                    {
                        var name = Path.GetFileName(file);
                        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            name = name[..^5];
                        }

                        if (name.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            name = name[prefix.Length..];
                        }

                        if (!string.IsNullOrEmpty(name))
                        {
                            keys.Add(name);
                        }
                    }
                }
            }

            var configAccounts = ConfigFactory.CurrentConfig?.TaskQueue?.OfType<StartUpTask>()
                .SelectMany(t => t.AccountNames ?? [])
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(SanitizeAccountKey) ?? [];
            foreach (var key in configAccounts)
            {
                keys.Add(key);
            }

            DataAccountList = [.. keys.Select(k => new GenericCombinedData<string>(GetDataAccountDisplayName(k), k))];
            foreach (var key in keys)
            {
                _knownDataAccountKeys.Add(key);
            }

            // 集合替换后重新解析当前选项引用, 保证 ComboBox.SelectedItem 在新集合中命中
            _currentDataAccountOption = _dataAccountList.FirstOrDefault(i => i.Value == _currentDataAccountKey);
            NotifyOfPropertyChange(nameof(SelectedDataAccountOption));
            NotifyOfPropertyChange(nameof(CurrentDataAccountDisplayName));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[DataAccount] failed to refresh data account list");
        }
    }

    private string GetDataAccountDisplayName(string accountKey)
    {
        if (accountKey == JsonDataKey.DefaultDataAccount)
        {
            return LocalizationHelper.GetString("DataAccountDefault");
        }

        foreach (var baseKey in new[] { JsonDataKey.OperBoxData, JsonDataKey.DepotData })
        {
            var json = JsonDataHelper.Get(AccountDataBucketKey(baseKey, accountKey), string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                var raw = JObject.Parse(json)["account"]?.ToString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return raw;
                }
            }
            catch
            {
                // 桶文件损坏时回落到文件名
            }
        }

        return accountKey;
    }

    /// <summary>
    /// 保存后若当前桶是新桶则刷新下拉列表。
    /// </summary>
    public void RegisterCurrentBucketIfNew()
    {
        if (_knownDataAccountKeys.Contains(_currentDataAccountKey) == false)
        {
            RefreshDataAccountList();
        }
    }

    /// <summary>
    /// 主文件 SaveDepotDetails 钩子: 在 details 写入 account 字段。
    /// </summary>
    public void StampDepotAccountField(JObject details)
    {
        if (!string.IsNullOrEmpty(_currentDataAccountRaw))
        {
            details["account"] = _currentDataAccountRaw;
        }
    }

    /// <summary>
    /// 主文件 SaveOperBoxDetails 钩子: 在 data 写入 account 字段。
    /// </summary>
    public void StampOperBoxAccountField(JObject data)
    {
        if (!string.IsNullOrEmpty(_currentDataAccountRaw))
        {
            data["account"] = _currentDataAccountRaw;
        }
    }

    /// <summary>
    /// 主文件 OnDepotCompleted 钩子: 当前账号桶无基线时丢弃掉落增量。
    /// 返回 true 表示应丢弃 (主文件早退)。
    /// </summary>
    public bool ShouldSkipDepotDropsForEmptyBucket()
    {
        if (DepotResult.Count == 0 && LastDepotSyncTime == null)
        {
            _logger.Information("Depot drop update skipped: no baseline for account bucket {Account}", _currentDataAccountKey);
            return true;
        }

        return false;
    }

    #endregion AccountScopedRecognitionData (feat/account-scoped-recognition-data)
}