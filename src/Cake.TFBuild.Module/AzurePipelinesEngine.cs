﻿using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Common.Build;
using Cake.Common.Build.AzurePipelines.Data;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Module.Shared;

namespace Cake.AzurePipelines.Module
{
    using Cake.Common.Build.AzurePipelines.Data;

    /// <summary>
    /// Represents a Cake engine for use with the Azure Pipelines engine.
    /// </summary>
    public sealed class AzurePipelinesEngine : CakeEngineBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AzurePipelinesEngine"/> type.
        /// </summary>
        /// <param name="dataService"></param>
        /// <param name="log">The log.</param>
        public AzurePipelinesEngine(ICakeDataService dataService, ICakeLog log) : base(new CakeEngine(dataService, log))
        {
            _engine.Setup += BuildSetup;
            _engine.TaskSetup += OnTaskSetup;
            _engine.TaskTeardown += OnTaskTeardown;
            _engine.Teardown += OnBuildTeardown;
        }

        private void OnBuildTeardown(object sender, TeardownEventArgs e)
        {
            var b = e.TeardownContext.BuildSystem();
            if (b.IsRunningOnPipelines())
            {
                b.AzurePipelines.Commands.UpdateRecord(_parentRecord, new AzurePipelinesRecordData
                {
                    FinishTime = DateTime.Now,
                    Status = AzurePipelinesTaskStatus.Completed,
                    Result = e.TeardownContext.Successful ? AzurePipelinesTaskResult.Succeeded : AzurePipelinesTaskResult.Failed,
                    Progress = GetProgress(TaskRecords.Count, _engine.Tasks.Count),
                });
            }
        }

        private void OnTaskTeardown(object sender, TaskTeardownEventArgs e)
        {
            var b = e.TaskTeardownContext.BuildSystem();
            if (b.IsRunningOnPipelines())
            {
                var currentTask = _engine.Tasks.First(t => t.Name == e.TaskTeardownContext.Task.Name);
                var currentIndex = _engine.Tasks.ToList().IndexOf(currentTask);
                //b.AzurePipelines.UpdateProgress(_parentRecord, GetProgress(currentIndex, _engine.Tasks.Count));
                var g = TaskRecords[currentTask.Name];
                b.AzurePipelines.Commands.UpdateRecord(g,
                    new AzurePipelinesRecordData
                    {
                        FinishTime = DateTime.Now,
                        Progress = 100,
                        Result = GetTaskResult(e.TaskTeardownContext)
                    });
            }
        }

        private AzurePipelinesTaskResult? GetTaskResult(ITaskTeardownContext taskTeardownContext)
        {
            if (taskTeardownContext.Skipped) return AzurePipelinesTaskResult.Skipped;

            // TODO: this logic should be improved but is difficult without task status in the context
            return AzurePipelinesTaskResult.Succeeded;
        }

        private void OnTaskSetup(object sender, TaskSetupEventArgs e)
        {
            var b = e.TaskSetupContext.BuildSystem();
            if (b.IsRunningOnPipelines())
            {
                var currentTask =
                    _engine.Tasks.First(t => t.Name == e.TaskSetupContext.Task.Name);
                var currentIndex = _engine.Tasks.ToList().IndexOf(currentTask);
                b.AzurePipelines.UpdateProgress(_parentRecord, GetProgress(currentIndex, _engine.Tasks.Count));
                //b.AzurePipelines.Commands.SetProgress(GetProgress(currentIndex, _engine.Tasks.Count), e.TaskSetupContext.Task.Name);
                b.AzurePipelines.Commands.SetProgress(GetProgress(currentIndex, _engine.Tasks.Count), string.Empty);
                var g = e.TaskSetupContext.AzurePipelines()
                    .Commands.CreateNewRecord(currentTask.Name, "build", TaskRecords.Count + 1,
                        new AzurePipelinesRecordData() {StartTime = DateTime.Now, ParentRecord = _parentRecord, Progress = 0});
                TaskRecords.Add(currentTask.Name, g);
            }
        }

        private int GetProgress(int currentTask, int count)
        {
            var f = (double) currentTask / (double) count * 100;
            return Convert.ToInt32(Math.Truncate(f));
        }

        private void BuildSetup(object sender, SetupEventArgs e)
        {
            var b = e.Context.BuildSystem();
            if (b.IsRunningOnPipelines())
            {
                //e.Context.AzurePipelines().Commands.SetProgress(0, "Build Setup");
                e.Context.AzurePipelines().Commands.SetProgress(0, string.Empty);
                var g = e.Context.AzurePipelines()
                    .Commands.CreateNewRecord("Cake Build", "build", 0, new AzurePipelinesRecordData {StartTime = DateTime.Now});
                _parentRecord = g;
            }
        }

        private Guid _parentRecord;
        private Dictionary<string, Guid> TaskRecords { get; } = new Dictionary<string, Guid>();
    }
}