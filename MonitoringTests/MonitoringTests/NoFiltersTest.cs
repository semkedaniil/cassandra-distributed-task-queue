﻿using System;

using NUnit.Framework;

using RemoteQueue.Handling;

using SKBKontur.Catalogue.RemoteTaskQueue.MonitoringTests.TestBases;
using SKBKontur.Catalogue.RemoteTaskQueue.TaskDatas;

namespace SKBKontur.Catalogue.RemoteTaskQueue.MonitoringTests.MonitoringTests
{
    public class NoFiltersTest : MonitoringFunctionalTestBase
    {
        public override void SetUp()
        {
            base.SetUp();
            remoteTaskQueue = container.Get<IRemoteTaskQueue>();
        }

        [Test]
        public void Test()
        {
            var ids = new string[10];
            for(var i = 0; i < 10; i++)
                ids[i] = AddTask(new SimpleTaskData());
            foreach(var id in ids)
                Console.WriteLine(id);
            var tasksListPage = LoadTasksListPage();
            tasksListPage = tasksListPage.RefreshUntilTaskRowIsPresent(10);
            Console.WriteLine();
            for(var i = 0; i < 10; i++)
            {
                var item = tasksListPage.GetTaskListItem(i);
                item.TaskId.WaitText(ids[9 - i]);
                item.TaskState.WaitText("Finished");
                item.TaskName.WaitText("SimpleTaskData");
                item.Attempts.WaitText("1");
                var details = tasksListPage.GoToTaskDetails(i);
                details.TaskId.WaitText(ids[9 - i]);
                details.TaskState.WaitText("Finished");
                details.TaskName.WaitText("SimpleTaskData");
                details.Attempts.WaitText("1");
                tasksListPage = details.GoToTasksListPage();
            }
        }

        [Test]
        public void EmptyListTest()
        {
            var tasksListPage = LoadTasksListPage();
            tasksListPage.GetTaskListItem(0).WaitAbsence();
        }

        private string AddTask<T>(T taskData) where T : ITaskData
        {
            return remoteTaskQueue.CreateTask(taskData).Queue();
        }

        private IRemoteTaskQueue remoteTaskQueue;
    }
}