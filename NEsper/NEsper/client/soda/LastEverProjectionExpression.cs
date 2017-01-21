///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;

namespace com.espertech.esper.client.soda
{
    /// <summary>
    /// Represents the "lastever" aggregation function.
    /// </summary>
    [Serializable]
    public class LastEverProjectionExpression : ExpressionBase
    {
        /// <summary>
        /// Ctor.
        /// </summary>
        public LastEverProjectionExpression()
        {
        }
    
        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="isDistinct">true for distinct</param>
        public LastEverProjectionExpression(bool isDistinct)
        {
            IsDistinct = isDistinct;
        }
    
        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="expression">to aggregate</param>
        /// <param name="isDistinct">true for distinct</param>
        public LastEverProjectionExpression(Expression expression, bool isDistinct)
        {
            IsDistinct = isDistinct;
            Children.Add(expression);
        }

        public override ExpressionPrecedenceEnum Precedence
        {
            get { return ExpressionPrecedenceEnum.UNARY; }
        }

        public override void ToPrecedenceFreeEPL(TextWriter writer)
        {
            RenderAggregation(writer, "lastever", IsDistinct, Children);
        }

        /// <summary>
        /// Returns true for distinct.
        /// </summary>
        /// <value>boolean indicating distinct or not</value>
        public bool IsDistinct { get; set; }
    }
}
