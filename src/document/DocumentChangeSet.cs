// Immutable description of one successful document commit. Raised only after authoritative state is
// installed; rejected operations never produce a change set. One object change per commit.
public sealed record DocumentChangeSet(long MutationSerial, DocumentObjectChange Change);
