// <copyright file="TaskQueueViewModel.AccountCycle.cs" company="MaaAssistantArknights">
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
using System.Linq;
using MaaWpfGui.Configuration.Factory;
using MaaWpfGui.Configuration.Single.MaaTask;
using MaaWpfGui.Constants;
using MaaWpfGui.Constants.Enums;
using MaaWpfGui.Helper;
using MaaWpfGui.Main;
using static MaaWpfGui.Main.AsstProxy;
using MaaWpfGui.Models;
using MaaWpfGui.Models.AsstTasks;
using MaaWpfGui.ViewModels.Items;

namespace MaaWpfGui.ViewModels.UI;

/// <summary>
/// 账号轮换 partial class —— 把 feat/account_rotation、feat/defer-rogue、fix/account_rotation/*、
/// fix/account-cycle-start-race、fix/account-cycle-fault-tolerance、feat/account-scoped-recognition-data
/// 等多个 fork 功能集中在 <see cref="TaskQueueViewModel.AdvanceAccountCycle"/> 上, 与上游主线
/// 解耦, 减少与 upstream/master-v2 合并时的冲突面 (downstream [HOT] 详见 docs/downstream-changes.md)。
/// </summary>
public partial class TaskQueueViewModel
{
    /// <summary>
    /// fix/account_rotation/6: 当前轮换账号名, 供左侧任务面板 Header 显示。
    /// 切号时随 <see cref="AdvanceAccountCycle"/> / <see cref="LinkStart"/> 更新;
    /// 非轮换模式或轮换结束时为空 (Header 隐藏)。
    /// </summary>
    public string? CurrentCycleAccountName { get => field; set => SetAndNotify(ref field, value); }

    /// <summary>
    /// 账号轮换推进：标记当前账号完成，取下一个账号，无缝继续执行，不产生"已停止"语义。
    /// feat/defer-rogue: 改为按预构建的扁平步骤列表推进，相邻步骤跨账号时显式补一个 StartUp(StartGame=false) 切号。
    /// fix/defer-rogue/1: 把"标记上一个步骤完成"抽成 <see cref="MarkPreviousStepCompleted"/>,并在
    /// 步骤耗尽 (nextStep == null) 的早退分支前调用,确保最后一个账号也会被打勾。
    /// </summary>
    public void AdvanceAccountCycle()
    {
        if (!StartUpTask.IsCycling)
        {
            return;
        }

        StartUpTask.AdvanceStepIndex();
        var nextStep = StartUpTask.CurrentStep;

        // fix/defer-rogue/1: 在早退分支前捕获 prevStep,保证最后一步也能被标记完成
        var prevStep = StartUpTask.GetPreviousStep();

        // fix/account_rotation/修改次数: 入口日志
        _logger.Information("[CycleAdv] stepIdx={Idx}, prev={PrevAcct}:{PrevPhase}, next={NextAcct}:{NextPhase}, stepsTotal={Total}",
            StartUpTask.CurrentStepIndex,
            prevStep?.AccountName, prevStep?.Phase,
            nextStep?.AccountName, nextStep?.Phase,
            StartUpTask.CurrentStepCount);

        // 步骤耗尽: 全部账号(及 Phase 2,如启用)已跑完
        if (nextStep == null)
        {
            MarkPreviousStepCompleted(prevStep);
            StartUpTask.IsCycling = false;
            CurrentCycleAccountName = string.Empty;
            _consecutiveEmptySteps = 0;
            _runningState.SetIdle(true);
            AddLog(LocalizationHelper.GetString("AccountCycleAllDone"), UiLogColor.Info);
            return;
        }

        var cfg = ConfigFactory.CurrentConfig.TaskQueue.OfType<StartUpTask>().FirstOrDefault();
        if (cfg == null)
        {
            StartUpTask.IsCycling = false;
            CurrentCycleAccountName = string.Empty;
            _runningState.SetIdle(true);
            return;
        }

        cfg.AccountSwitchEnabled = true;
        cfg.AccountName = nextStep.AccountName?.Trim() ?? string.Empty;
        CurrentCycleAccountName = nextStep.AccountName?.Trim() ?? string.Empty;

        // feat/account-scoped-recognition-data: 切号即切换识别数据桶,
        // 清上一账号脏数据 + 预载本账号桶, 堵住 StageDrops 掉落增量以旧账号库存为基数的合并路径
        Instances.ToolboxViewModel.SwitchDataAccount(cfg.AccountName);

        // 标记前一个步骤所属账号完成 (仅当跨账号或离开 Phase 2 时)
        MarkPreviousStepCompleted(prevStep);

        // fix/account_rotation/6: 切换到新步骤前重置左侧任务列表状态 + 进度计数,
        // 对齐 LinkStartWithTasks:1909-1910, 否则上一账号的绿色(Completed)会残留,
        // 进度条也会因 MainTasksCompletedCount 不归零而不再出现。
        MainTasksCompletedCount = 0;
        ResetTaskItemStatuses();

        // 跨账号切换: 显式追加一个 StartUp(StartGame=false) 切号
        bool needStartupSwitch = prevStep == null || prevStep.AccountName?.Trim() != nextStep.AccountName?.Trim();

        // 轮换推进：不重连模拟器、不重启游戏，仅切号 + 跑任务
        _runningState.SetStopping(false);
        AddLog($"{LocalizationHelper.GetString("AccountCycleSwitchingTo")}{(nextStep.AccountName?.Trim() ?? string.Empty)} (Phase {nextStep.Phase} idx={StartUpTask.CurrentStepIndex}/{StartUpTask.CurrentStepCount}{(needStartupSwitch ? " (switch)" : string.Empty)})", UiLogColor.Info);

        bool taskRet = true;
        int count = 0;
        bool lateStageOn = StartUpTask.LateStageRogueAndReclamation;
        int currentPhase = nextStep.Phase;

        try
        {
            // 1) 显式切号
            if (needStartupSwitch)
            {
                var switchTask = new AsstStartUpTask
                {
                    ClientType = SettingsViewModel.GameSettings.ClientType,
                    StartGame = false,
                    AccountName = cfg.AccountName,
                };
                var (isSuccess, taskId) = Instances.AsstProxy.AsstAppendTaskWithEncoding(TaskType.StartUp, switchTask);
                if (isSuccess && taskId > 0)
                {
                    ++count;

                    // fix/account_rotation/6: 把切号 taskId 绑回 StartUp 行。
                    // 循环内 needStartupSwitch 分支会 continue 跳过 StartUp (行 2313),
                    // 导致该行 _taskIds 永远指向首账号旧 taskId, 新事件 IndexOf 返回 -1 被丢弃。
                    var startUpTaskEntry = ConfigFactory.CurrentConfig.TaskQueue.FirstOrDefault(t => t.TaskType == TaskType.StartUp);
                    int startUpIndex = startUpTaskEntry != null ? ConfigFactory.CurrentConfig.TaskQueue.IndexOf(startUpTaskEntry) : -1;
                    if (startUpIndex >= 0 && startUpIndex < TaskItemViewModels.Count)
                    {
                        TaskItemViewModels[startUpIndex].SetTaskIds([taskId]);
                    }
                }
                else
                {
                    taskRet = false;
                    AddLog($"StartUp switch failed for account={cfg.AccountName}", UiLogColor.Error);
                }
            }

            // 2) Phase 任务 (fix/account_rotation/修改次数: 改用 for 循环避免 IndexOf 在重复项时返回错误索引)
            for (int i = 0; i < ConfigFactory.CurrentConfig.TaskQueue.Count; i++)
            {
                var item = ConfigFactory.CurrentConfig.TaskQueue[i];
                int index = i;
                if (!IsTaskEnable(item))
                {
                    if (index >= 0 && index < TaskItemViewModels.Count)
                    {
                        TaskItemViewModels[index].StatusDisplay = TaskItemStatus.Skipped;
                    }

                    continue;
                }

                // Phase 过滤 (LateStage OFF 时 no-op)
                if (lateStageOn && !IsInCurrentPhase(item.TaskType, currentPhase))
                {
                    continue;
                }

                // 已显式补过 StartUp, 跳过循环内 StartUp 处理避免重复
                if (needStartupSwitch && item.TaskType == TaskType.StartUp)
                {
                    continue;
                }

                try
                {
                    if (item.TaskType == TaskType.StartUp)
                    {
                        var startUpTask = new AsstStartUpTask
                        {
                            ClientType = SettingsViewModel.GameSettings.ClientType,
                            StartGame = false,
                            AccountName = cfg.AccountName,
                        };
                        var (isSuccess, taskId) = Instances.AsstProxy.AsstAppendTaskWithEncoding(TaskType.StartUp, startUpTask);
                        if (isSuccess && taskId > 0)
                        {
                            ++count;
                            TaskItemViewModels.ElementAtOrDefault(index)?.SetTaskIds([taskId]);
                        }
                        else
                        {
                            taskRet = false;
                            AddLog(LocalizationHelper.GetStringFormat("TaskAppend.Error", LocalizationHelper.GetString(item.TaskType.ToString()), item.NameOrTaskType), UiLogColor.Error);
                        }
                    }
                    else
                    {
                        var (isSuccess, taskIds) = SerializeTask(item);
                        switch (isSuccess)
                        {
                            case true:
                                ++count;
                                var idsList = taskIds as IList<int> ?? taskIds.ToList();
                                _logger.Information("[CycleAdv] Append task #{Idx} '{Name}' type={Type} taskIds=[{Ids}]", index, item.NameOrTaskType, item.TaskType, string.Join(",", idsList));
                                TaskItemViewModels.ElementAtOrDefault(index)?.SetTaskIds(idsList);
                                break;
                            case false:
                                taskRet = false;
                                AddLog(LocalizationHelper.GetStringFormat("TaskAppend.Error", LocalizationHelper.GetString(item.TaskType.ToString()), item.NameOrTaskType), UiLogColor.Error);
                                if (index >= 0 && index < TaskItemViewModels.Count)
                                {
                                    TaskItemViewModels[index].StatusDisplay = TaskItemStatus.Error;
                                }

                                break;
                            case null:
                                AddLog(LocalizationHelper.GetStringFormat("TaskAppend.Skip", LocalizationHelper.GetString(item.TaskType.ToString()), item.NameOrTaskType), UiLogColor.Info);
                                if (index >= 0 && index < TaskItemViewModels.Count)
                                {
                                    TaskItemViewModels[index].StatusDisplay = TaskItemStatus.Skipped;
                                }

                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    taskRet = false;
                    AddLog(LocalizationHelper.GetStringFormat("TaskAppend.Error", LocalizationHelper.GetString(item.TaskType.ToString()), item.NameOrTaskType) + "\n" + ex.Message, UiLogColor.Error);
                }
            }

            // fix/account_rotation/修改次数: 记录追加结果
            _logger.Information("[CycleAdv] phase={Phase}, needStartupSwitch={Switch}, count={Count}, taskRet={Ret}",
                currentPhase, needStartupSwitch, count, taskRet);

            // 3) 空步骤跳过 (fix/account-cycle-fault-tolerance: 加保险防止 RebuildCycleSteps 全空步骤
            //    时的无限循环. 实际触发需 RebuildCycleSteps 全部步骤均无任务且未步骤耗尽, 例如
            //    AccountNames 全空 + Phase 2 未配 LateStage 但 Phase 2 步骤仍生成. 保险阈值 64)
            //    成功追加任务时清零.
            if (count == 0)
            {
                if (++_consecutiveEmptySteps > 64)
                {
                    AddLog("[Cycle] Too many consecutive empty steps, stop cycle.", UiLogColor.Error);
                    StartUpTask.IsCycling = false;
                    _consecutiveEmptySteps = 0;
                    SetStopped(runStopScript: false);
                    return;
                }

                AddLog($"[Cycle] Step empty (Phase {currentPhase}), advancing to next...", UiLogColor.Info);
                AdvanceAccountCycle();
                return;
            }

            _consecutiveEmptySteps = 0;

            // fix/account-cycle-start-race: AllTasksCompleted 回调后 Core 工作线程仍处于
            // wait_for(task_delay) 睡眠窗口(约 500ms), 此时 AsstStart() 必返回 false
            // (Assistant::start 检查 !m_thread_idle), 但任务已入队、线程醒来会自行消费,
            // 因此只要 AsstRunning() 为 true 即视为启动成功, 不打断轮换。
            // 真失败(未连接/handle 失效)时 m_running 必为 false, 判定精确。
            // 注意: 与上游 merge 时此处可能冲突, 见 WORKFLOW.md §6 冲突手解清单。
            // 短路语义: taskRet=false(append 失败)时保持旧行为不调用 AsstStart, 直接停轮换。
            bool startOk = taskRet && (Instances.AsstProxy.AsstStart() || Instances.AsstProxy.AsstRunning());

            if (!taskRet || !startOk)
            {
                AddLog(LocalizationHelper.GetString("UnknownErrorOccurs"), UiLogColor.Error);
                StartUpTask.IsCycling = false;
                CurrentCycleAccountName = string.Empty;

                // 兜底: 清空 Core 队列, 避免真失败时已 append 的任务"幽灵执行"
                _ = Instances.AsstProxy.AsstStop();
                SetStopped(runStopScript: false);
            }
        }
        catch (Exception ex)
        {
            AddLog($"[Cycle] AdvanceAccountCycle error: {ex.Message}", UiLogColor.Error);
            StartUpTask.IsCycling = false;
            CurrentCycleAccountName = string.Empty;
            _runningState.SetIdle(true);
        }
    }

    /// <summary>
    /// feat/defer-rogue: 判断 taskType 是否属于当前 phase。Phase 1 = 除 Roguelike/Reclamation 外;
    /// Phase 2 = 仅 Roguelike/Reclamation。
    /// </summary>
    private static bool IsInCurrentPhase(TaskType taskType, int phase)
    {
        return phase switch
        {
            1 => taskType != TaskType.Roguelike && taskType != TaskType.Reclamation,
            2 => taskType == TaskType.Roguelike || taskType == TaskType.Reclamation,
            _ => true,
        };
    }

    /// <summary>
    /// fix/defer-rogue/1: 将"标记上一个步骤所属账号为已完成"抽成独立方法,供 <see cref="AdvanceAccountCycle"/>
    /// 在普通推进路径与"步骤耗尽"早退分支两处复用。语义保持与原始 inline 块一致:
    /// 仅当上一阶段是 Phase 2,或未启用 LateStageRogueAndReclamation 时,才把该账号打勾。
    /// </summary>
    private void MarkPreviousStepCompleted(AccountCycleStep? prevStep)
    {
        if (prevStep == null || string.IsNullOrEmpty(prevStep.AccountName))
        {
            return;
        }

        bool leftPhase2 = prevStep.Phase == 2;
        bool lateStageOff = !StartUpTask.LateStageRogueAndReclamation;
        if (leftPhase2 || lateStageOff)
        {
            StartUpTask.MarkAccountCompleted(prevStep.AccountName);
        }
    }

    /// <summary>
    /// fix/account-cycle-fault-tolerance: 连续空步骤计数 (advance 触发 count==0 时递增, 成功追加任务时清零).
    /// 防止 RebuildCycleSteps 生成全空步骤列表时的递归死循环.
    /// </summary>
    private int _consecutiveEmptySteps;

    /// <summary>
    /// fix/account_rotation + feat/defer-rogue + feat/account-scoped-recognition-data:
    /// 把 <see cref="TaskQueueViewModel.LinkStart"/> 中插入的 ~50 行 fork 逻辑全部下沉到 partial class,
    /// 主文件 LinkStart 收敛为: wait → PrepareCycleStart → LinkStartWithTasks → release。
    /// 返回 true 表示调用方应继续走 LinkStartWithTasks;返回 false 表示已处理(轮换早退/全部完成)。
    /// </summary>
    public bool PrepareCycleStart()
    {
        var startUpConfig = StartUpTask;

        // fix/defer-rogue/1: 防止轮换中途再次触发 LinkStart (Stop 后再次点击 / 定时器 / 快捷键),
        // 避免 InitAccountCycleItems + RebuildCycleSteps 重置进度导致步骤丢失或重复。
        if (startUpConfig.IsCycling)
        {
            return false;
        }

        startUpConfig.InitAccountCycleItems();

        if (startUpConfig.AccountCycleEnabled && startUpConfig.AccountCycleItems.Any(x => x.IsSelected && !string.IsNullOrEmpty(x.AccountName)))
        {
            // 轮换模式：每次 LinkStart 只处理一个步骤 (account, phase)
            startUpConfig.RebuildCycleSteps();
            var firstStep = startUpConfig.CurrentStep;

            startUpConfig.IsCycling = true;
            if (firstStep != null)
            {
                var cfg = ConfigFactory.CurrentConfig.TaskQueue.OfType<StartUpTask>().FirstOrDefault();
                if (cfg != null)
                {
                    cfg.AccountSwitchEnabled = true;
                    cfg.AccountName = firstStep.AccountName?.Trim() ?? string.Empty;
                    CurrentCycleAccountName = firstStep.AccountName?.Trim() ?? string.Empty;
                    AddLog($"{LocalizationHelper.GetString("AccountCycleSwitchingTo")}{(firstStep.AccountName?.Trim() ?? string.Empty)} (Phase {firstStep.Phase})", UiLogColor.Info);
                }
                else
                {
                    startUpConfig.IsCycling = false;
                }
            }
            else
            {
                AddLog(LocalizationHelper.GetString("AccountCycleAllDone"), UiLogColor.Info);
                startUpConfig.IsCycling = false;
            }
        }
        else
        {
            startUpConfig.ResetCycle();
            CurrentCycleAccountName = string.Empty;
        }

        // feat/account-scoped-recognition-data: 运行前锚定干员/仓库识别数据桶到本次账号
        // (轮换=首账号, 非轮换=配置账号, 无账号名时回落 _default 桶)
        Instances.ToolboxViewModel.SwitchDataAccount(ConfigFactory.CurrentConfig.TaskQueue.OfType<StartUpTask>().FirstOrDefault()?.AccountName);

        return true;
    }

    /// <summary>
    /// fix/account_rotation/修改次数 + fix/account_rotation/6:
    /// 把 <see cref="TaskQueueViewModel.SetStopped"/> 中插入的轮换状态保护 ~18 行下沉到 partial。
    /// 返回 false 表示轮换继续(SetStopped 应早退);返回 true 表示已清理轮换,SetStopped 应继续原逻辑。
    /// 末尾的 CurrentCycleAccountName 清空也由本方法处理(原位 SetStopped 末尾)。
    /// </summary>
    public bool HandleStopping(bool runStopScript)
    {
        if (StartUpTask.IsCycling)
        {
            if (_runningState.GetIdle())
            {
                StartUpTask.IsCycling = false;
            }
            else if (runStopScript && _runningState.GetStopping())
            {
                StartUpTask.IsCycling = false;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// fix/account_rotation/6: SetStopped 末尾清理当前账号显示 (轮换继续的早退分支已提前 return, 不会走到这里)。
    /// </summary>
    public void ClearCurrentCycleAccountName()
    {
        CurrentCycleAccountName = string.Empty;
    }

    /// <summary>
    /// feat/defer-rogue: LinkStartWithTasks 内的 LateStage 阶段过滤钩子。lateStageOn=false 时直接放行。
    /// 主文件 foreach 仅保留: 现有 IsTaskEnable 检查 → 调本钩子 → 原 try/SerializeTask 块。
    /// </summary>
    public bool ShouldSkipByPhase(BaseTask item, bool lateStageOn, int currentPhase)
    {
        return lateStageOn && !IsInCurrentPhase(item.TaskType, currentPhase);
    }

    /// <summary>
    /// fix/account_rotation: AsstProxy AllTasksCompleted 回调处的轮换推进钩子。
    /// 返回 true 表示已轮换推进,调用方应 break 跳出 AllTasksCompleted handler;
    /// 返回 false 表示非轮换/已结束/已空闲,调用方继续走原 SetIdle(true) 流程。
    /// </summary>
    public bool OnAllTasksCompleted(bool isIdle)
    {
        if (StartUpTask.IsCycling && !isIdle)
        {
            AdvanceAccountCycle();
            return StartUpTask.IsCycling;
        }

        return false;
    }
}