// <copyright file="RecruitTask.Expedite.cs" company="MaaAssistantArknights">
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
using Newtonsoft.Json;

namespace MaaWpfGui.Configuration.Single.MaaTask;

/// <summary>
/// feat/expedite-threshold partial class.
/// 加急门槛 <see cref="ExpediteMinLevel"/> + 计算属性 <see cref="ExpediteMode"/> 下沉, 主文件保留 UseExpedited 字段。
/// 减少与 upstream/master-v2 合并时主文件的冲突面 (下游 [HOT] 详见 docs/downstream-changes.md)。
/// </summary>
public partial class RecruitTask
{
    /// <summary>
    /// Gets or sets 加急招募门槛：仅当组合最低星级 ≥ 此值时使用加急许可
    /// 0 = 所有星级均加急（默认，向后兼容）；4 / 5 / 6 = 对应星级阈值
    /// </summary>
    public int ExpediteMinLevel { get; set; } = 0;

    /// <summary>
    /// Gets or sets 加急招募模式（合并总开关与星级门槛）由 ViewModel 维护：
    /// 0 = 不使用加急；1 = 所有星级均加急；4 / 5 / 6 = 仅对应星级及以上使用加急
    /// </summary>
    [JsonIgnore]
    public int ExpediteMode
    {
        get
        {
            if (UseExpedited is false)
            {
                return 0;
            }

            return ExpediteMinLevel == 0 ? 1 : ExpediteMinLevel;
        }
        set
        {
            var expedited = value != 0;
            UseExpedited = expedited;
            ExpediteMinLevel = value switch
            {
                0 => 0,
                1 => 0,
                int v => v,
            };
        }
    }
}