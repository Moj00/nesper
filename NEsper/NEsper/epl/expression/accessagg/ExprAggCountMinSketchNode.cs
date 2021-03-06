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
using com.espertech.esper.client.util;
using com.espertech.esper.compat;
using com.espertech.esper.compat.collections;
using com.espertech.esper.core.service;
using com.espertech.esper.epl.agg.service;
using com.espertech.esper.epl.approx;
using com.espertech.esper.epl.core;
using com.espertech.esper.epl.expression.baseagg;
using com.espertech.esper.epl.expression.core;
using com.espertech.esper.epl.table.mgmt;
using com.espertech.esper.events;
using com.espertech.esper.util;

namespace com.espertech.esper.epl.expression.accessagg
{
    /// <summary>
    /// Represents the Count-min sketch aggregate function.
    /// </summary>
    [Serializable]
    public class ExprAggCountMinSketchNode 
        : ExprAggregateNodeBase 
        , ExprAggregateAccessMultiValueNode
    {
        private const double DEFAULT__EPS_OF_TOTAL_COUNT = 0.0001;
        private const double DEFAULT__CONFIDENCE = 0.99;
        private const int DEFAULT__SEED = 1234567;

        private static readonly CountMinSketchAgentStringUTF16 DEFAULT__AGENT = new CountMinSketchAgentStringUTF16();
    
        private const string MSG_NAME = "Count-min-sketch";
        private const string NAME__EPS_OF_TOTAL_COUNT = "epsOfTotalCount";
        private const string NAME__CONFIDENCE = "confidence";
        private const string NAME__SEED = "seed";
        private const string NAME__TOPK = "topk";
        private const string NAME__AGENT = "agent";
    
        private readonly CountMinSketchAggType _aggType;
    
        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="distinct">flag indicating unique or non-unique value aggregation</param>
        public ExprAggCountMinSketchNode(bool distinct, CountMinSketchAggType aggType)
            : base(distinct)
        {
            _aggType = aggType;
        }
    
        public override AggregationMethodFactory ValidateAggregationChild(ExprValidationContext validationContext)
        {
            return ValidateAggregationInternal(validationContext, null);
        }
    
        public AggregationMethodFactory ValidateAggregationParamsWBinding(ExprValidationContext context, TableMetadataColumnAggregation tableAccessColumn)
        {
            return ValidateAggregationInternal(context, tableAccessColumn);
        }

        public override string AggregationFunctionName
        {
            get { return _aggType.GetFuncName(); }
        }

        protected override bool EqualsNodeAggregateMethodOnly(ExprAggregateNode node)
        {
            return false;
        }

        public CountMinSketchAggType AggType
        {
            get { return _aggType; }
        }

        public EventType GetEventTypeCollection(EventAdapterService eventAdapterService, int statementId)
        {
            return null;
        }
    
        public ICollection<EventBean> EvaluateGetROCollectionEvents(EventBean[] eventsPerStream, bool isNewData, ExprEvaluatorContext context)
        {
            return null;
        }

        public Type ComponentTypeCollection
        {
            get { return null; }
        }

        public ICollection<object> EvaluateGetROCollectionScalar(EventBean[] eventsPerStream, bool isNewData, ExprEvaluatorContext context)
        {
            return null;
        }
    
        public EventType GetEventTypeSingle(EventAdapterService eventAdapterService, int statementId) {
            return null;
        }
    
        public EventBean EvaluateGetEventBean(EventBean[] eventsPerStream, bool isNewData, ExprEvaluatorContext context) {
            return null;
        }

        protected override bool IsExprTextWildcardWhenNoParams
        {
            get { return false; }
        }

        private AggregationMethodFactory ValidateAggregationInternal(ExprValidationContext context, TableMetadataColumnAggregation tableAccessColumn)
        {
            if (IsDistinct) {
                throw new ExprValidationException(MessagePrefix + "is not supported with distinct");
            }
    
            // for declaration, validate the specification and return the state factory
            if (_aggType == CountMinSketchAggType.STATE) {
                if (context.ExprEvaluatorContext.StatementType != StatementType.CREATE_TABLE) {
                    throw new ExprValidationException(MessagePrefix + "can only be used in create-table statements");
                }
                var specification = ValidateSpecification(context.ExprEvaluatorContext, context.EngineImportService);
                var stateFactory = context.EngineImportService.AggregationFactoryFactory.MakeCountMinSketch(context.StatementExtensionSvcContext, this, specification);
                return new ExprAggCountMinSketchNodeFactoryState(stateFactory);
            }
    
            // validate number of parameters
            if (_aggType == CountMinSketchAggType.ADD || _aggType == CountMinSketchAggType.FREQ) {
                if (ChildNodes.Length == 0 || ChildNodes.Length > 1) {
                    throw new ExprValidationException(MessagePrefix + "requires a single parameter expression");
                }
            }
            else {
                if (ChildNodes.Length != 0) {
                    throw new ExprValidationException(MessagePrefix + "requires a no parameter expressions");
                }
            }
    
            // validate into-table and table-access
            if (_aggType == CountMinSketchAggType.ADD) {
                if (context.IntoTableName == null) {
                    throw new ExprValidationException(MessagePrefix + "can only be used with into-table");
                }
            }
            else {
                if (tableAccessColumn == null) {
                    throw new ExprValidationException(MessagePrefix + "requires the use of a table-access expression");
                }
                ExprNodeUtility.GetValidatedSubtree(ExprNodeOrigin.AGGPARAM, ChildNodes, context);
            }
    
            // obtain evaluator
            ExprEvaluator addOrFrequencyEvaluator = null;
            if (_aggType == CountMinSketchAggType.ADD || _aggType == CountMinSketchAggType.FREQ) {
                addOrFrequencyEvaluator = ChildNodes[0].ExprEvaluator;
            }
    
            return new ExprAggCountMinSketchNodeFactoryUse(this, addOrFrequencyEvaluator);
        }
    
        private CountMinSketchSpec ValidateSpecification(ExprEvaluatorContext exprEvaluatorContext, EngineImportService engineImportService) {
            // default specification
            var spec = new CountMinSketchSpec(new CountMinSketchSpecHashes(DEFAULT__EPS_OF_TOTAL_COUNT, DEFAULT__CONFIDENCE, DEFAULT__SEED), null, DEFAULT__AGENT);
    
            // no parameters
            if (ChildNodes.Length == 0) {
                return spec;
            }
    
            // check expected parameter type: a json object
            if (ChildNodes.Length > 1 || !(ChildNodes[0] is ExprConstantNode)) {
                throw DeclaredWrongParameterExpr;
            }
            var constantNode = (ExprConstantNode) ChildNodes[0];
            var value = constantNode.GetConstantValue(exprEvaluatorContext);
            if (!(value is IDictionary<string, object>)) {
                throw DeclaredWrongParameterExpr;
            }
    
            // define what to populate
            var descriptors = new PopulateFieldWValueDescriptor[] {
                new PopulateFieldWValueDescriptor(NAME__EPS_OF_TOTAL_COUNT, typeof(double), spec.HashesSpec.GetType(), vv => {
                        if (vv != null) {spec.HashesSpec.EpsOfTotalCount = (double) vv;}
                    }, true),
                new PopulateFieldWValueDescriptor(NAME__CONFIDENCE, typeof(double), spec.HashesSpec.GetType(), vv => {
                        if (vv != null) {spec.HashesSpec.Confidence = (double) vv;}
                    }, true),
                new PopulateFieldWValueDescriptor(NAME__SEED, typeof(int), spec.HashesSpec.GetType(), vv =>  {
                        if (vv != null) {spec.HashesSpec.Seed = (int) vv;}
                    }, true),
                new PopulateFieldWValueDescriptor(NAME__TOPK, typeof(int), spec.GetType(), vv => {
                        if (vv != null) {spec.TopkSpec = (int) vv;}
                    }, true),
                new PopulateFieldWValueDescriptor(NAME__AGENT, typeof(string), spec.GetType(), vv =>  {
                        if (vv != null) {
                            CountMinSketchAgent transform;
                            try {
                                var transformClass = engineImportService.ResolveType((string) vv, false);
                                transform = TypeHelper.Instantiate<CountMinSketchAgent>(transformClass);
                            }
                            catch (Exception e) {
                                throw new ExprValidationException("Failed to instantiate agent provider: " + e.Message, e);
                            }
                            spec.Agent = transform;
                        }
                    }, true),
            };
    
            // populate from json, validates incorrect names, coerces types, instantiates transform
            PopulateUtil.PopulateSpecCheckParameters(descriptors, (IDictionary<String, object>) value, spec, engineImportService);
    
            return spec;
        }

        public ExprValidationException DeclaredWrongParameterExpr
        {
            get
            {
                return new ExprValidationException(
                    MessagePrefix + " expects either no parameter or a single json parameter object");
            }
        }

        private string MessagePrefix
        {
            get { return MSG_NAME + " aggregation function '" + _aggType.GetFuncName() + "' "; }
        }
    }
}
