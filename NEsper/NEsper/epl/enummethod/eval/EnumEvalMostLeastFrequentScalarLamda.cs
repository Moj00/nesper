///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

using com.espertech.esper.client;
using com.espertech.esper.compat.collections;
using com.espertech.esper.epl.expression.core;
using com.espertech.esper.epl.expression;
using com.espertech.esper.events.arr;

namespace com.espertech.esper.epl.enummethod.eval
{
    public class EnumEvalMostLeastFrequentScalarLamda
        : EnumEvalBase
        , EnumEval
    {
        private readonly bool _isMostFrequent;
        private readonly ObjectArrayEventType _resultEventType;

        public EnumEvalMostLeastFrequentScalarLamda(ExprEvaluator innerExpression, int streamCountIncoming, bool mostFrequent, ObjectArrayEventType resultEventType)
            : base(innerExpression, streamCountIncoming)
        {
            _isMostFrequent = mostFrequent;
            _resultEventType = resultEventType;
        }

        public Object EvaluateEnumMethod(EventBean[] eventsLambda, ICollection<object> target, bool isNewData, ExprEvaluatorContext context)
        {

            var items = new LinkedHashMap<Object, int?>();
            var values = target;

            var resultEvent = new ObjectArrayEventBean(new Object[1], _resultEventType);

            foreach (Object next in values)
            {

                resultEvent.Properties[0] = next;
                eventsLambda[StreamNumLambda] = resultEvent;

                var item = InnerExpression.Evaluate(new EvaluateParams(eventsLambda, isNewData, context));

                int? existing;
                if (!items.TryGetValue(item, out existing))
                {
                    existing = 1;
                }
                else
                {
                    existing++;
                }
                items.Put(item, existing);
            }

            return EnumEvalMostLeastFrequentEvent.GetResult(items, _isMostFrequent);
        }
    }
}
