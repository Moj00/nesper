///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.client;
using com.espertech.esper.compat.threading;
using com.espertech.esper.epl.expression.core;
using com.espertech.esper.epl.expression;
using com.espertech.esper.epl.variable;

namespace com.espertech.esper.filter
{
    /// <summary>
    /// Adapter for use by <seealso cref="FilterParamIndexBooleanExpr"/> to evaluate bool expressions, providing events per stream to expression nodes. Generated by @{link FilterSpecParamExprNode} for bool expression filter parameters.
    /// </summary>
    public class ExprNodeAdapterMultiStream : ExprNodeAdapterBaseVariables
    {
        private readonly EventBean[] _prototypeArray;

        private readonly IThreadLocal<EventBean[]> _arrayPerThread;

        public ExprNodeAdapterMultiStream(int filterSpecId, int filterSpecParamPathNum, ExprNode exprNode, ExprEvaluatorContext evaluatorContext, VariableService variableService, EventBean[] prototype)
            : base(filterSpecId, filterSpecParamPathNum, exprNode, evaluatorContext, variableService)
         {
            _prototypeArray = prototype;
            _arrayPerThread = ThreadLocalManager.Create(
                () =>
                {
                    var eventsPerStream = new EventBean[_prototypeArray.Length];
                    Array.Copy(_prototypeArray, 0, eventsPerStream, 0, _prototypeArray.Length);
                    return eventsPerStream;
                });
        }

        protected EventBean[] PrototypeArray
        {
            get { return _prototypeArray; }
        }

        public override bool Evaluate(EventBean theEvent)
        {
            if (VariableService != null)
            {
                VariableService.SetLocalVersion();
            }
            EventBean[] eventsPerStream = _arrayPerThread.GetOrCreate();
            eventsPerStream[0] = theEvent;
            return EvaluatePerStream(eventsPerStream);
        }
    }
}
