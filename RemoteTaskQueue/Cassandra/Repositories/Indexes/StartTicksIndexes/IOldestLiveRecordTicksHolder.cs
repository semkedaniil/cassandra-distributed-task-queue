using RemoteQueue.Cassandra.Entities;

namespace RemoteQueue.Cassandra.Repositories.Indexes.StartTicksIndexes
{
    public interface IOldestLiveRecordTicksHolder
    {
        long? TryStartReadToEndSession(TaskState taskState);
        void TryMoveForward(TaskState taskState, long newTicks);
        void MoveBackwardIfNecessary(TaskState taskState, long newTicks);
    }
}