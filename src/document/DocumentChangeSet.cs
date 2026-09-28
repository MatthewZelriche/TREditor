// Immutable description of one successful document commit. Raised only after authoritative state is
// installed; rejected operations never produce a change set. A multi-object commit emits one change
// set per object, all carrying the same mutation serial.
public sealed record DocumentChangeSet(long MutationSerial, DocumentObjectChange Change);
