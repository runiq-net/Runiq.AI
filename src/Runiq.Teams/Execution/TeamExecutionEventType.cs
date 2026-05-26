namespace Runiq.Teams.Models.Execution;

/// <summary>
/// Agent team yürütmesi sırasında oluşabilecek event türlerini belirtir.
/// </summary>
public enum TeamExecutionEventType
{
    /// <summary>
    /// Takım yürütmesinin başladığını belirtir.
    /// </summary>
    TeamStarted = 0,

    /// <summary>
    /// Takım üyesi agent yürütmesinin başladığını belirtir.
    /// </summary>
    MemberStarted = 1,

    /// <summary>
    /// Takım üyesi agent tarafından üretilen kısmi cevabı belirtir.
    /// </summary>
    MemberDelta = 2,

    /// <summary>
    /// Takım üyesi agent yürütmesinin tamamlandığını belirtir.
    /// </summary>
    MemberCompleted = 3,

    /// <summary>
    /// Takım üyesi agent yürütmesinin hata aldığını belirtir.
    /// </summary>
    MemberFailed = 4,

    /// <summary>
    /// Takım yürütmesinin tamamlandığını belirtir.
    /// </summary>
    TeamCompleted = 5,

    /// <summary>
    /// Takım yürütmesinin hata aldığını belirtir.
    /// </summary>
    TeamFailed = 6,

    /// <summary>
    /// Takım üyesi agent tarafından bir tool çağrısının başlatıldığını belirtir.
    /// </summary>
    MemberToolCallStarted = 7,

    /// <summary>
    /// Takım üyesi agent tarafından bir tool çağrısının tamamlandığını belirtir.
    /// </summary>
    MemberToolCallCompleted = 8,

    /// <summary>
    /// Takım üyesi agent tarafından bir tool çağrısının hata aldığını belirtir.
    /// </summary>
    MemberToolCallFailed = 9
}
