// <copyright file="CopilotViewModel.CopilotSet.cs" company="MaaAssistantArknights">
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
using System.Threading.Tasks;
using System.Windows;
using JetBrains.Annotations;

namespace MaaWpfGui.ViewModels.UI;

/// <summary>
/// feat/copilot-paste-clipboard partial class.
/// 旧格式 maa:// 作业集支持: <see cref="IsAmbiguousCopilotCode"/> + <see cref="PasteClipboardCopilotSet"/> 下沉,
/// 主文件 <see cref="CopilotViewModel"/> 仅保留 maa:// 旧格式注释 (TODO 标记迁移完后删除)。
/// 减少与 upstream/master-v2 合并时主文件的冲突面 (下游 [HOT] 详见 docs/downstream-changes.md)。
/// </summary>
public partial class CopilotViewModel
{
    // TODO: 作业站迁移完成后删除此方法（旧格式 maa:// 和纯数字无法区分类型，届时所有格式都自带类型信息）

    /// <summary>
    /// 判断是否为类型不明确的旧格式代码（maa:// 或纯数字，无法区分作业/作业集）
    /// </summary>
    private static bool IsAmbiguousCopilotCode(string value)
    {
        return value.StartsWith(CopilotIdPrefix, StringComparison.OrdinalIgnoreCase)
            || int.TryParse(value, out _);
    }

    // TODO: 作业站迁移完成后删除此方法及对应的 XAML 按钮（CopilotView.xaml Grid.Column=3）、
    // TooltipBlock（Grid.Column=3）、本地化字符串 PasteClipboardCopilotSetTip

    /// <summary>
    /// Paste clipboard contents.
    /// UI 绑定的方法
    /// </summary>
    /// <returns>Task</returns>
    [UsedImplicitly]
    public async Task PasteClipboardCopilotSet()
    {
        if (!Clipboard.ContainsText())
        {
            return;
        }

        var text = Clipboard.GetText().Trim();

        // 新格式自带类型信息，交给 Filename → UpdateFileDoc 自动路由
        // 旧格式（maa:// / 纯数字）类型不明确，按按钮上下文当作业集处理
        if (!IsAmbiguousCopilotCode(text))
        {
            Filename = text;
            return;
        }

        StartEnabled = false;
        ClearLog();
        await GetCopilotSetAsync(text);
        CopilotUrl = CopilotUiUrl;
        StartEnabled = true;
    }
}