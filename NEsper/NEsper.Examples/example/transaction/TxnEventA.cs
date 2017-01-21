///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////


using System;

namespace NEsper.Example.Transaction
{
    public class TxnEventA : TxnEventBase
    {
        private String customerId;

        public TxnEventA(String transactionId, long timestamp, String customerId)
            : base(transactionId, timestamp)
        {
            this.customerId = customerId;
        }

        public String CustomerId
        {
            get { return customerId; }
        }

        public override String ToString()
        {
            return base.ToString() + " customerId=" + customerId;
        }
    }
}
