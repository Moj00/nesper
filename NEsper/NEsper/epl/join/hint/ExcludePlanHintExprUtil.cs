///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.client;
using com.espertech.esper.compat.collections;
using com.espertech.esper.core.context.mgr;
using com.espertech.esper.core.service;
using com.espertech.esper.epl.declexpr;
using com.espertech.esper.epl.expression.core;
using com.espertech.esper.epl.spec;
using com.espertech.esper.epl.table.mgmt;
using com.espertech.esper.events;
using com.espertech.esper.events.arr;
using com.espertech.esper.pattern;

namespace com.espertech.esper.epl.join.hint
{
    public class ExcludePlanHintExprUtil
    {
        protected readonly static ObjectArrayEventType OAEXPRESSIONTYPE;
    
        static ExcludePlanHintExprUtil()
        {
            var properties = new LinkedHashMap<String, Object>();
            properties.Put("from_streamnum", typeof(int));
            properties.Put("to_streamnum", typeof(int));
            properties.Put("from_streamname", typeof(String));
            properties.Put("to_streamname", typeof(String));
            properties.Put("opname", typeof(String));
            properties.Put("exprs", typeof(String[]));

            OAEXPRESSIONTYPE = new ObjectArrayEventType(
                    EventTypeMetadata.CreateAnonymous(typeof (ExcludePlanHintExprUtil).Name),
                    typeof (ExcludePlanHintExprUtil).Name, 
                    0,
                    null,
                    properties, 
                    null, 
                    null, 
                    null);
        }

        public static EventBean ToEvent(
            int fromStreamNum,
            int toStreamNum,
            String fromStreamName,
            String toStreamName,
            String opname,
            ExprNode[] expressions)
        {
            var texts = new String[expressions.Length];
            for (var i = 0; i < expressions.Length; i++) {
                texts[i] = ExprNodeUtility.ToExpressionStringMinPrecedenceSafe(expressions[i]);
            }
            var @event = new Object[] {fromStreamNum, toStreamNum, fromStreamName, toStreamName, opname, texts};
            return new ObjectArrayEventBean(@event, OAEXPRESSIONTYPE);
        }
    
        public static ExprEvaluator ToExpression(String hint, StatementContext statementContext)
        {
            var toCompile = "select * from System.Object.win:time(" + hint + ")";
            var raw = EPAdministratorHelper.CompileEPL(toCompile, hint, false, null,
                    SelectClauseStreamSelectorEnum.ISTREAM_ONLY, statementContext.EngineImportService,
                    statementContext.VariableService, statementContext.SchedulingService,
                    statementContext.EngineURI, statementContext.ConfigSnapshot,
                    new PatternNodeFactoryImpl(), new ContextManagementServiceImpl(),
                    new ExprDeclaredServiceImpl(),
                    new TableServiceImpl());
            var expr = raw.StreamSpecs[0].ViewSpecs[0].ObjectParameters[0];
            var validated = ExprNodeUtility.ValidateSimpleGetSubtree(ExprNodeOrigin.HINT, expr, statementContext, OAEXPRESSIONTYPE, false);
            return validated.ExprEvaluator;
        }
    }
}
