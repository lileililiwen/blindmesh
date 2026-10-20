namespace Blindmesh.Domain.Tasks;

/// <summary>
/// The lifecycle state of a participation. The state machine of a participation
/// is its own: it never shares a state with a task, a slot, a submission, a
/// finding or a dispute. An applied participation holds no slot; an accepted
/// participation holds exactly one exclusive lease.
/// </summary>
public enum ParticipationState
{
    /// <summary>The contributor applied. The application holds no slot yet.</summary>
    Applied,

    /// <summary>
    /// The owner accepted the application. The participation holds one
    /// exclusive lease and the contributor has to start within the start window.
    /// </summary>
    Accepted,

    /// <summary>The contributor started the work. The execution deadline is visible.</summary>
    Running,

    /// <summary>The owner declined the application. It never held a slot.</summary>
    Declined,

    /// <summary>The contributor withdrew before the work started.</summary>
    Withdrawn,

    /// <summary>A deadline of the participation passed.</summary>
    Expired,
}
