using System.Text.Json.Serialization;

namespace XFEExtension.NetCore.XUnit.Runtime;

/// <summary>
/// 指定 XFE 命令行界面使用的语言。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConsoleLanguage>))]
public enum ConsoleLanguage
{
    /// <summary>
    /// 根据当前用户界面区域自动选择语言；中文区域使用简体中文，其他区域使用英文。
    /// </summary>
    Auto,

    /// <summary>
    /// 使用英文命令行界面。
    /// </summary>
    English,

    /// <summary>
    /// 使用简体中文命令行界面。
    /// </summary>
    Chinese
}
