// <copyright file="StartUpSettingsUserControlModel.AccountCycle.cs" company="MaaAssistantArknights">
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
using System.ComponentModel;
using System.Linq;
using System.Windows;
using HandyControl.Controls;
using JetBrains.Annotations;
using MaaWpfGui.Configuration.Factory;
using MaaWpfGui.Configuration.Single.MaaTask;
using MaaWpfGui.Constants;
using MaaWpfGui.Constants.Enums;
using MaaWpfGui.Helper;
using MaaWpfGui.Main;
using MaaWpfGui.Models;
using MaaWpfGui.Models.AsstTasks;
using MaaWpfGui.ViewModels.Orchestration;
using MaaWpfGui.ViewModels.UI;
using Stylet;
using static MaaWpfGui.Main.AsstProxy;

namespace MaaWpfGui.ViewModels.UserControl.TaskQueue;

/// <summary>
/// feat/account_rotation + feat/account-cycle-refactor + fix/account-rotation-supersede-switcher partial class.
/// 把账号轮换配置 UI 交互全部从主文件下沉, 主文件仅保留 CycleConfig + InitAccountCycleItems() 调用。
/// 减少与 upstream/master-v2 合并时主文件的冲突面 (下游 [HOT] 详见 docs/downstream-changes.md)。
/// </summary>
public partial class StartUpSettingsUserControlModel
{

    // fix/account-rotation-supersede-switcher: 删除 #region Account Switch (Single) 整段 (AccountName /
    // AccountSwitchEnabled / AccountSwitchManualRun 共 ~60 行). 账号轮换彻底吸收账号切换的生态位;
    // StartUpTask.AccountName / AccountSwitchEnabled 字段仍保留 (向后兼容旧 GUI 配置 + 轮换切号仍依赖).

    private readonly ObservableCollection<AccountCycleItem> _accountCycleItems = [];

    /// <summary>
    /// feat/account-cycle-refactor: 委托给 <see cref="AccountCycleOrchestrator.Instance"/> 管理
    /// 步骤列表 / 完成集合 / IsCycling 等轮换状态. 本类仅负责 UI 项列表 (<c>_accountCycleItems</c>)
    /// 与勾选/编辑交互.
    /// </summary>
    private static AccountCycleOrchestrator Orchestrator => AccountCycleOrchestrator.Instance;

    public bool AccountCycleEnabled
    {
        get => CycleConfig?.AccountCycleEnabled ?? true;
        set
        {
            if (CycleConfig == null)
            {
                return;
            }

            CycleConfig.AccountCycleEnabled = value;
            if (!value)
            {
                ResetCycle();
            }

            NotifyOfPropertyChange();
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether 是否将肉鸽 (Roguelike) 与生息演算 (Reclamation) 任务延后到所有账号的基础任务完成后执行。
    /// 仅在 <see cref="AccountCycleEnabled"/> 为 true 时生效。
    /// </summary>
    public bool LateStageRogueAndReclamation
    {
        get => CycleConfig?.LateStageRogueAndReclamation ?? false;
        set
        {
            if (CycleConfig == null)
            {
                return;
            }

            CycleConfig.LateStageRogueAndReclamation = value;
            NotifyOfPropertyChange();
        }
    }

    // fix/account-rotation-supersede-switcher: 删除 ShowEditSection / AccountCycleMode / ShowAddMode / ShowDeleteMode
    // 共 4 个字段/属性 + 2 个 backing field. UI 改为永久内联, 无需「编辑模式」二级开关.

    public ObservableCollection<AccountCycleItem> AccountCycleItems => _accountCycleItems;

    public void InitAccountCycleItems()
    {
        SyncAccountNamesToItems();
    }

    /// <summary>
    /// feat/account-cycle-refactor: 委托到 Orchestrator.
    /// </summary>
    public bool IsCycling
    {
        get => Orchestrator.IsCycling;
        set => Orchestrator.IsCycling = value;
    }

    /// <summary>feat/account-cycle-refactor: 委托到 Orchestrator.</summary>
    public int CurrentStepCount => Orchestrator.CurrentStepCount;

    /// <summary>feat/account-cycle-refactor: 委托到 Orchestrator.</summary>
    public int CurrentStepIndex => Orchestrator.CurrentStepIndex;

    /// <summary>feat/account-cycle-refactor: 委托到 Orchestrator.</summary>
    public AccountCycleStep? CurrentStep => Orchestrator.CurrentStep;

    /// <summary>feat/account-cycle-refactor: 委托到 Orchestrator.</summary>
    public AccountCycleStep? GetPreviousStep() => Orchestrator.GetPreviousStep();

    /// <summary>feat/account-cycle-refactor: 委托到 Orchestrator.</summary>
    public int CurrentPhase => Orchestrator.CurrentPhase;

    /// <summary>feat/account-cycle-refactor: 委托到 Orchestrator.</summary>
    public void RebuildCycleSteps() => Orchestrator.RebuildCycleSteps(
        _accountCycleItems
            .Where(x => x.IsSelected && !string.IsNullOrEmpty(x.AccountName))
            .OrderBy(x => x.Index)
            .Select(x => x.AccountName),
        LateStageRogueAndReclamation
            && ConfigFactory.CurrentConfig.TaskQueue.Any(t =>
                IsTaskEnable(t) &&
                (t.TaskType == TaskType.Roguelike || t.TaskType == TaskType.Reclamation)));

    /// <summary>feat/account-cycle-refactor: 委托到 Orchestrator.</summary>
    public void AdvanceStepIndex() => Orchestrator.AdvanceStepIndex();

    public void SyncAccountNamesToItems()
    {
        var config = CycleConfig;
        if (config == null)
        {
            // fix/account-cycle-config-source: 无 StartUp 任务时清空轮换列表 (避免 UI 残留)
            _accountCycleItems.Clear();
            NotifyOfPropertyChange(nameof(AccountCycleItems));
            return;
        }

        // fix/trim-account-name: 迁移清理历史脏数据 (账号名尾随空格/制表符/换行)
        // 清理后对首个受影响账号打 INFO 日志便于用户感知, 后续步骤不再感知
        // (MaaCore set_account 也已 Trim, 此处是配置层根治, 让 UI 也立即显示干净账号名)
        bool trimmedFirstAccount = false;
        for (int i = 0; i < config.AccountNames.Count; i++)
        {
            var original = config.AccountNames[i];
            var trimmed = original?.Trim();
            if (trimmed != original)
            {
                if (!trimmedFirstAccount && !string.IsNullOrEmpty(trimmed))
                {
                    Instances.TaskQueueViewModel.AddLog(
                        $"[fix/trim-account-name] AccountNames[{i}] 已去除首尾空白: \"{original}\" → \"{trimmed}\"",
                        UiLogColor.Info);
                    trimmedFirstAccount = true;
                }
                config.AccountNames[i] = trimmed ?? string.Empty;
            }
        }

        if (!string.IsNullOrEmpty(config.AccountName))
        {
            var originalAcctName = config.AccountName;
            var trimmedAcctName = originalAcctName.Trim();
            if (trimmedAcctName != originalAcctName)
            {
                Instances.TaskQueueViewModel.AddLog(
                    $"[fix/trim-account-name] AccountName 已去除首尾空白: \"{originalAcctName}\" → \"{trimmedAcctName}\"",
                    UiLogColor.Info);
                config.AccountName = trimmedAcctName;
            }
        }

        // fix/account-rotation-supersede-switcher: 向后兼容迁移, 旧 GUI 配置有 AccountName 但 AccountNames 空时
        // 自动将单账号名复制到轮换列表首项;若无单账号名则至少保留 2 行空项作为占位.
        if (config.AccountNames.Count > 0 && string.IsNullOrEmpty(config.AccountNames[0]) && !string.IsNullOrEmpty(config.AccountName))
        {
            config.AccountNames[0] = config.AccountName;
        }

        if (config.AccountNames.Count == 0 && !string.IsNullOrEmpty(config.AccountName))
        {
            config.AccountNames.Add(config.AccountName);
            config.AccountNames.Add(string.Empty);
        }

        var existingSelections = _accountCycleItems
            .Where(x => !string.IsNullOrEmpty(x.AccountName))
            .ToDictionary(x => x.AccountName, x => x.IsSelected);

        _accountCycleItems.Clear();

        // fix/account-cycle-fault-tolerance (C4): 重名校验, 保留首次出现, 后续同名取消勾选并提示
        var seenNames = new HashSet<string>(System.StringComparer.Ordinal);
        int duplicateCount = 0;
        for (int i = 0; i < config.AccountNames.Count; i++)
        {
            var name = config.AccountNames[i];
            bool isDuplicate = !string.IsNullOrEmpty(name) && !seenNames.Add(name);
            if (isDuplicate)
            {
                duplicateCount++;
            }

            var item = new AccountCycleItem
            {
                DisplayName = LocalizationHelper.GetString("AccountCycleNewAccountDefaultName") + (i + 1),
                AccountName = name,
                IsSelected = !isDuplicate && (existingSelections.TryGetValue(name, out var selected) ? selected : true),
                IsCompleted = Orchestrator.IsAccountCompleted(name),
                Index = i,
            };
            item.PropertyChanged += OnAccountCycleItemPropertyChanged;
            _accountCycleItems.Add(item);
        }

        if (duplicateCount > 0)
        {
            Instances.TaskQueueViewModel.AddLog($"[Cycle] Warning: {duplicateCount} duplicate account name(s) detected, duplicates have been deselected.", UiLogColor.Warning);
        }

        NotifyOfPropertyChange(nameof(AccountCycleItems));
    }

    private void OnAccountCycleItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not AccountCycleItem item || e.PropertyName != nameof(AccountCycleItem.AccountName))
        {
            return;
        }

        var config = CycleConfig;
        if (config != null && item.Index < config.AccountNames.Count)
        {
            config.AccountNames[item.Index] = item.AccountName;
            NotifyOfPropertyChange(nameof(AccountCycleItems));
        }
    }

    [UsedImplicitly]
    public void AddAccountAfter(AccountCycleItem currentItem)
    {
        var config = CycleConfig;
        if (config == null)
        {
            return;
        }

        int insertIndex = currentItem?.Index + 1 ?? config.AccountNames.Count;

        config.AccountNames.Insert(insertIndex, string.Empty);

        var newItem = new AccountCycleItem
        {
            DisplayName = LocalizationHelper.GetString("AccountCycleNewAccountDefaultName") + (insertIndex + 1),
            AccountName = string.Empty,
            IsSelected = true,
            IsCompleted = false,
            Index = insertIndex,
        };
        newItem.PropertyChanged += OnAccountCycleItemPropertyChanged;
        _accountCycleItems.Insert(insertIndex, newItem);

        RebuildIndexes();
        NotifyOfPropertyChange(nameof(AccountCycleItems));
    }

    [UsedImplicitly]
    public void RemoveAccount(AccountCycleItem item)
    {
        var config = CycleConfig;
        if (config == null)
        {
            return;
        }

        // fix/account-rotation-supersede-switcher: [✖] 按钮永久可见, 点击后弹 MessageBox 二次确认防误删.
        // 复用 MessageBoxHelper.Show (与 TaskQueueViewModel.RemoveTask:1568-1581 模式一致),
        // 文案 AccountCycleRemoveMessage 含 {0} 占位符 = 账号名, 标题 AccountCycleRemoveConfirm.
        var accountName = item.AccountName?.Trim() ?? string.Empty;
        var confirm = MessageBoxHelper.Show(
            LocalizationHelper.GetStringFormat("AccountCycleRemoveMessage", accountName),
            LocalizationHelper.GetString("AccountCycleRemoveConfirm"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        if (item.Index < config.AccountNames.Count)
        {
            config.AccountNames.RemoveAt(item.Index);
        }

        item.PropertyChanged -= OnAccountCycleItemPropertyChanged;
        _accountCycleItems.Remove(item);

        RebuildIndexes();
        NotifyOfPropertyChange(nameof(AccountCycleItems));
    }

    private void RebuildIndexes()
    {
        for (int i = 0; i < _accountCycleItems.Count; i++)
        {
            _accountCycleItems[i].Index = i;
            _accountCycleItems[i].DisplayName = LocalizationHelper.GetString("AccountCycleNewAccountDefaultName") + (i + 1);
        }
    }

    public string? GetCurrentCycleAccount()
    {
        return _accountCycleItems
            .Where(x => x.IsSelected && !x.IsCompleted && !string.IsNullOrEmpty(x.AccountName))
            .OrderBy(x => x.Index)
            .FirstOrDefault()?.AccountName;
    }

    /// <summary>feat/account-cycle-refactor: 委托到 Orchestrator, 同时更新 UI 项.</summary>
    public void MarkAccountCompleted(string accountName)
    {
        if (string.IsNullOrEmpty(accountName))
        {
            return;
        }

        Orchestrator.MarkAccountCompleted(accountName);
        var item = _accountCycleItems.FirstOrDefault(x => x.AccountName == accountName);
        if (item != null)
        {
            item.IsCompleted = true;
        }
    }

    /// <summary>feat/account-cycle-refactor: 委托到 Orchestrator.</summary>
    public void ResetCycle() => Orchestrator.ResetCycle();

    /// <summary>feat/account-cycle-refactor: 委托到 Orchestrator + 重置 UI 项.</summary>
    public void ClearCompletedAccounts()
    {
        Orchestrator.ClearCompletedAccounts();
        foreach (var item in _accountCycleItems)
        {
            item.IsCompleted = false;
        }
    }

}
