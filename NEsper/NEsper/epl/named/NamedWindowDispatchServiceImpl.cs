///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

using com.espertech.esper.client;
using com.espertech.esper.client.hook;
using com.espertech.esper.compat;
using com.espertech.esper.compat.collections;
using com.espertech.esper.compat.threading;
using com.espertech.esper.core.context.util;
using com.espertech.esper.core.service;
using com.espertech.esper.epl.metric;
using com.espertech.esper.epl.table.mgmt;
using com.espertech.esper.epl.variable;
using com.espertech.esper.events.vaevent;
using com.espertech.esper.metrics.instrumentation;
using com.espertech.esper.schedule;
using com.espertech.esper.timer;
using com.espertech.esper.util;

namespace com.espertech.esper.epl.named
{
	/// <summary>
	/// This service hold for each named window a dedicated processor and a lock to the named window.
	/// This lock is shrared between the named window and on-delete statements.
	/// </summary>
	public class NamedWindowDispatchServiceImpl : NamedWindowDispatchService
	{
	    private readonly SchedulingService _schedulingService;
	    private readonly VariableService _variableService;
	    private readonly TableService _tableService;
	    private readonly ExceptionHandlingService _exceptionHandlingService;
	    private readonly bool _isPrioritized;
	    private readonly IReaderWriterLock _eventProcessingRwLock;
	    private readonly MetricReportingService _metricReportingService;

        private readonly IThreadLocal<IList<NamedWindowConsumerLatch>> _threadLocal = ThreadLocalManager.Create<IList<NamedWindowConsumerLatch>>(
	        () => new List<NamedWindowConsumerLatch>());
        
        private readonly IThreadLocal<IDictionary<EPStatementAgentInstanceHandle, object>> _dispatchesPerStmtTl = ThreadLocalManager.Create<IDictionary<EPStatementAgentInstanceHandle, object>>(
            () => new Dictionary<EPStatementAgentInstanceHandle, object>());

        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="schedulingService">The scheduling service.</param>
        /// <param name="variableService">is for variable access</param>
        /// <param name="tableService">The table service.</param>
        /// <param name="isPrioritized">if the engine is running with prioritized execution</param>
        /// <param name="eventProcessingRWLock">The event processing rw lock.</param>
        /// <param name="exceptionHandlingService">The exception handling service.</param>
        /// <param name="metricReportingService">The metric reporting service.</param>
	    public NamedWindowDispatchServiceImpl(
	        SchedulingService schedulingService,
	        VariableService variableService,
	        TableService tableService,
	        bool isPrioritized,
	        IReaderWriterLock eventProcessingRWLock,
	        ExceptionHandlingService exceptionHandlingService,
	        MetricReportingService metricReportingService)
	    {
	        _schedulingService = schedulingService;
	        _variableService = variableService;
	        _tableService = tableService;
	        _isPrioritized = isPrioritized;
	        _eventProcessingRwLock = eventProcessingRWLock;
	        _exceptionHandlingService = exceptionHandlingService;
	        _metricReportingService = metricReportingService;
	    }

	    public NamedWindowProcessor CreateProcessor(
	        string name,
	        NamedWindowMgmtServiceImpl namedWindowMgmtService,
	        NamedWindowDispatchService namedWindowDispatchService,
	        string contextName,
	        EventType eventType,
	        StatementResultService statementResultService,
	        ValueAddEventProcessor revisionProcessor,
	        string eplExpression,
	        string statementName,
	        bool isPrioritized,
	        bool isEnableSubqueryIndexShare,
	        bool enableQueryPlanLog,
	        MetricReportingService metricReportingService,
	        bool isBatchingDataWindow,
	        bool isVirtualDataWindow,
	        ICollection<string> optionalUniqueKeyProps,
	        string eventTypeAsName,
	        StatementContext statementContextCreateWindow)
	    {
	        return new NamedWindowProcessor(
	            name, namedWindowMgmtService, namedWindowDispatchService, contextName, eventType, statementResultService,
	            revisionProcessor, eplExpression, statementName, isPrioritized, isEnableSubqueryIndexShare,
	            enableQueryPlanLog, metricReportingService, isBatchingDataWindow, isVirtualDataWindow,
	            optionalUniqueKeyProps, eventTypeAsName, statementContextCreateWindow);
	    }

	    public NamedWindowTailView CreateTailView(
	        EventType eventType,
	        NamedWindowMgmtService namedWindowMgmtService,
	        NamedWindowDispatchService namedWindowDispatchService,
	        StatementResultService statementResultService,
	        ValueAddEventProcessor revisionProcessor,
	        bool prioritized,
	        bool parentBatchWindow,
	        string contextName,
	        TimeSourceService timeSourceService,
	        ConfigurationEngineDefaults.Threading threadingConfig)
        {
	        return new NamedWindowTailView(eventType, namedWindowMgmtService, namedWindowDispatchService, statementResultService, revisionProcessor, _isPrioritized, parentBatchWindow, timeSourceService, threadingConfig);
	    }

	    public void Dispose()
	    {
	        _threadLocal.Dispose();
	        _dispatchesPerStmtTl.Dispose();
	    }

	    public void AddDispatch(
	        NamedWindowConsumerLatchFactory latchFactory,
	        NamedWindowDeltaData delta,
	        IDictionary<EPStatementAgentInstanceHandle, IList<NamedWindowConsumerView>> consumers)
	    {
	        _threadLocal.GetOrCreate().Add(
	            latchFactory.NewLatch(delta, consumers));
	    }

	    public bool Dispatch()
	    {
            var dispatches = _threadLocal.GetOrCreate();
	        if (dispatches.IsEmpty())
	        {
	            return false;
	        }

	        while (!dispatches.IsEmpty())
            {
	            // Acquire main processing lock which locks out statement management
	            if (InstrumentationHelper.ENABLED) { InstrumentationHelper.Get().QNamedWindowDispatch(_exceptionHandlingService.EngineURI);}

                try
                {
                    using (_eventProcessingRwLock.AcquireReadLock())
                    {
                        try
                        {
                            var units = dispatches.ToArray();
                            dispatches.Clear();
                            ProcessDispatches(units);
                        }
                        catch (Exception ex)
                        {
                            throw new EPException(ex);
                        }
                    }
                }
                finally
                {
                    if (InstrumentationHelper.ENABLED) { InstrumentationHelper.Get().ANamedWindowDispatch(); }
                }
            }

	        return true;
	    }

	    private void ProcessDispatches(NamedWindowConsumerLatch[] dispatches)
        {
	        if (dispatches.Length == 1)
	        {
	            var latch = dispatches[0];
	            try {
	                latch.Await();
	                var newData = latch.DeltaData.NewData;
	                var oldData = latch.DeltaData.OldData;

	                foreach (var entry in latch.DispatchTo)
                    {
	                    var handle = entry.Key;

	                    handle.StatementHandle.MetricsHandle.Call(
                            _metricReportingService.PerformanceCollector, 
                            () => { ProcessHandle(handle, entry.Value, newData, oldData); });

	                    if ((_isPrioritized) && (handle.IsPreemptive)) {
	                        break;
	                    }
	                }
	            }
	            finally {
	                latch.Done();
	            }

	            return;
	        }

	        // Multiple different-result dispatches to same or different statements are needed in two situations:
	        // a) an event comes in, triggers two insert-into statements inserting into the same named window and the window produces 2 results
	        // b) a time batch is grouped in the named window, and a timer fires for both groups at the same time producing more then one result
	        // c) two on-merge/update/delete statements fire for the same arriving event each updating the named window

	        // Most likely all dispatches go to different statements since most statements are not joins of
	        // named windows that produce results at the same time. Therefore sort by statement handle.
	        var dispatchesPerStmt = _dispatchesPerStmtTl.GetOrCreate();
	        for (int ii = 0; ii < dispatches.Length; ii++)
	        {
	            var latch = dispatches[ii];
	            latch.Await();
	            foreach (var entry in latch.DispatchTo)
	            {
	                var handle = entry.Key;
	                var perStmtObj = dispatchesPerStmt.Get(handle);
	                if (perStmtObj == null)
	                {
	                    dispatchesPerStmt.Put(handle, latch);
	                }
	                else if (perStmtObj is IList<NamedWindowConsumerLatch>)
	                {
	                    var list = (IList<NamedWindowConsumerLatch>) perStmtObj;
	                    list.Add(latch);
	                }
	                else // convert from object to list
	                {
	                    var unitObj = (NamedWindowConsumerLatch) perStmtObj;
	                    IList<NamedWindowConsumerLatch> list = new List<NamedWindowConsumerLatch>();
	                    list.Add(unitObj);
	                    list.Add(latch);
	                    dispatchesPerStmt.Put(handle, list);
	                }
	            }
	        }

	        try {
	            // Dispatch - with or without metrics reporting
	            foreach (var entry in dispatchesPerStmt) {
	                var handle = entry.Key;
	                var perStmtObj = entry.Value;

	                // dispatch of a single result to the statement
	                if (perStmtObj is NamedWindowConsumerLatch) {
	                    var unit = (NamedWindowConsumerLatch) perStmtObj;
	                    var newData = unit.DeltaData.NewData;
	                    var oldData = unit.DeltaData.OldData;

	                    handle.StatementHandle.MetricsHandle.Call(
	                        _metricReportingService.PerformanceCollector,
	                        () => ProcessHandle(handle, unit.DispatchTo.Get(handle), newData, oldData));

	                    if ((_isPrioritized) && (handle.IsPreemptive)) {
	                        break;
	                    }

	                    continue;
	                }

	                // dispatch of multiple results to a the same statement, need to aggregate per consumer view
	                var deltaPerConsumer = GetDeltaPerConsumer(perStmtObj, handle);
                    handle.StatementHandle.MetricsHandle.Call(
                        _metricReportingService.PerformanceCollector,
                        () => ProcessHandleMultiple(handle, deltaPerConsumer));

	                if ((_isPrioritized) && (handle.IsPreemptive)) {
	                    break;
	                }
	            }
	        }
	        finally
            {
	            for (int ii = 0; ii < dispatches.Length; ii++)
	            {
	                dispatches[ii].Done();
	            }
	        }

	        dispatchesPerStmt.Clear();
	    }

	    private void ProcessHandleMultiple(EPStatementAgentInstanceHandle handle, IDictionary<NamedWindowConsumerView, NamedWindowDeltaData> deltaPerConsumer)
        {
	        if (InstrumentationHelper.ENABLED) { InstrumentationHelper.Get().QNamedWindowCPMulti(_exceptionHandlingService.EngineURI, deltaPerConsumer, handle, _schedulingService.Time);}

            try
            {
	            using (handle.StatementAgentInstanceLock.AcquireWriteLock())
	            {
	                try
	                {
	                    if (handle.HasVariables)
	                    {
	                        _variableService.SetLocalVersion();
	                    }
	                    foreach (var entryDelta in deltaPerConsumer)
	                    {
	                        var newData = entryDelta.Value.NewData;
	                        var oldData = entryDelta.Value.OldData;
	                        entryDelta.Key.Update(newData, oldData);
	                    }

	                    // internal join processing, if applicable
	                    handle.InternalDispatch();
	                }
	                catch (Exception ex)
	                {
	                    _exceptionHandlingService.HandleException(ex, handle, ExceptionHandlerExceptionType.PROCESS);
	                }
	                finally
	                {
	                    if (handle.HasTableAccess)
	                    {
	                        _tableService.TableExprEvaluatorContext.ReleaseAcquiredLocks();
	                    }
	                }
	            }
            }
            finally
            {
                if (InstrumentationHelper.ENABLED) { InstrumentationHelper.Get().ANamedWindowCPMulti(); }
            }
        }

	    private void ProcessHandle(EPStatementAgentInstanceHandle handle, IList<NamedWindowConsumerView> value, EventBean[] newData, EventBean[] oldData)
        {
	        if (InstrumentationHelper.ENABLED) { InstrumentationHelper.Get().QNamedWindowCPSingle(_exceptionHandlingService.EngineURI, value, newData, oldData, handle, _schedulingService.Time);}

	        try
	        {
	            using (handle.StatementAgentInstanceLock.AcquireWriteLock())
	            {
	                try
	                {
	                    if (handle.HasVariables)
	                    {
	                        _variableService.SetLocalVersion();
	                    }

	                    foreach (var consumerView in value)
	                    {
	                        consumerView.Update(newData, oldData);
	                    }

	                    // internal join processing, if applicable
	                    handle.InternalDispatch();
	                }
	                catch (Exception ex)
	                {
	                    _exceptionHandlingService.HandleException(ex, handle, ExceptionHandlerExceptionType.PROCESS);
	                }
	                finally
	                {
	                    if (handle.HasTableAccess)
	                    {
	                        _tableService.TableExprEvaluatorContext.ReleaseAcquiredLocks();
	                    }
	                }
	            }
	        }
	        finally
	        {
                if (InstrumentationHelper.ENABLED) { InstrumentationHelper.Get().ANamedWindowCPSingle(); }
	        }
	    }

	    public IDictionary<NamedWindowConsumerView, NamedWindowDeltaData> GetDeltaPerConsumer(object perStmtObj, EPStatementAgentInstanceHandle handle)
        {
	        var list = (IList<NamedWindowConsumerLatch>) perStmtObj;
	        var deltaPerConsumer = new LinkedHashMap<NamedWindowConsumerView, NamedWindowDeltaData>();
	        foreach (var unit in list)   // for each unit
	        {
	            foreach (var consumerView in unit.DispatchTo.Get(handle))   // each consumer
	            {
	                var deltaForConsumer = deltaPerConsumer.Get(consumerView);
	                if (deltaForConsumer == null)
	                {
	                    deltaPerConsumer.Put(consumerView, unit.DeltaData);
	                }
	                else
	                {
	                    var aggregated = new NamedWindowDeltaData(deltaForConsumer, unit.DeltaData);
	                    deltaPerConsumer.Put(consumerView, aggregated);
	                }
	            }
	        }
	        return deltaPerConsumer;
	    }
	}
} // end of namespace
