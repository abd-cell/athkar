namespace Athkar.Shareds.Enums;

/// <summary>Who a reminder or broadcast is aimed at.</summary>
public enum Audience
{
    /// <summary>Every device that still accepts notifications.</summary>
    All = 0,

    /// <summary>Devices whose chosen language matches the target code.</summary>
    Language = 1,

    /// <summary>Devices on one platform — used for store-specific notices, rarely for content.</summary>
    Platform = 2,

    /// <summary>One device, by key. Exists for testing a message before it goes wide.</summary>
    Device = 3,
}
