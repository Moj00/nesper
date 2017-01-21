///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.client;
using com.espertech.esper.client.scopetest;
using com.espertech.esper.compat;
using com.espertech.esper.compat.collections;
using com.espertech.esper.compat.logging;
using com.espertech.esper.metrics.instrumentation;
using com.espertech.esper.support.bean;
using com.espertech.esper.support.client;

using NUnit.Framework;

namespace com.espertech.esper.regression.resultset
{
    [TestFixture]
	public class TestAggregateRowPerEventDistinct 
	{
        private const string SYMBOL_DELL = "DELL";
        private const string SYMBOL_IBM = "IBM";

        private EPServiceProvider _epService;
	    private SupportUpdateListener _testListener;

        [SetUp]
	    public void SetUp()
	    {
	        _testListener = new SupportUpdateListener();
	        _epService = EPServiceProviderManager.GetDefaultProvider(SupportConfigFactory.GetConfiguration());
	        _epService.Initialize();
	        if (InstrumentationHelper.ENABLED) { InstrumentationHelper.StartTest(_epService, this.GetType(), this.GetType().FullName);}
	    }

        [TearDown]
	    public void TearDown() {
	        if (InstrumentationHelper.ENABLED) { InstrumentationHelper.EndTest();}
	        _testListener = null;
	    }

        [Test]
	    public void TestSumOneView()
	    {
	        // Every event generates a new row, this time we sum the price by symbol and output volume
	        string viewExpr = "select irstream symbol, sum(distinct volume) as volSum " +
	                          "from " + typeof(SupportMarketDataBean).FullName + ".win:length(3) ";

	        EPStatement selectTestView = _epService.EPAdministrator.CreateEPL(viewExpr);
	        selectTestView.AddListener(_testListener);

	        // assert select result type
	        Assert.AreEqual(typeof(string), selectTestView.EventType.GetPropertyType("symbol"));
	        Assert.AreEqual(typeof(long?), selectTestView.EventType.GetPropertyType("volSum"));

	        SendEvent(SYMBOL_DELL, 10000);
	        AssertEvents(SYMBOL_DELL, 10000);

	        SendEvent(SYMBOL_DELL, 10000);
	        AssertEvents(SYMBOL_DELL, 10000);       // still 10k since summing distinct volumes

	        SendEvent(SYMBOL_DELL, 20000);
	        AssertEvents(SYMBOL_DELL, 30000);

	        SendEvent(SYMBOL_IBM, 1000);
	        AssertEvents(SYMBOL_DELL, 31000, SYMBOL_IBM, 31000);

	        SendEvent(SYMBOL_IBM, 1000);
	        AssertEvents(SYMBOL_DELL, 21000, SYMBOL_IBM, 21000);

	        SendEvent(SYMBOL_IBM, 1000);
	        AssertEvents(SYMBOL_DELL, 1000, SYMBOL_IBM, 1000);
	    }

	    private void AssertEvents(string symbol, long volSum)
	    {
	        EventBean[] oldData = _testListener.LastOldData;
	        EventBean[] newData = _testListener.LastNewData;

	        Assert.IsNull(oldData);
	        Assert.AreEqual(1, newData.Length);

	        Assert.AreEqual(symbol, newData[0].Get("symbol"));
	        Assert.AreEqual(volSum, newData[0].Get("volSum"));

	        _testListener.Reset();
	        Assert.IsFalse(_testListener.IsInvoked);
	    }

	    private void AssertEvents(string symbolOld, long volSumOld,
	                              string symbolNew, long volSumNew)
	    {
	        EventBean[] oldData = _testListener.LastOldData;
	        EventBean[] newData = _testListener.LastNewData;

	        Assert.AreEqual(1, oldData.Length);
	        Assert.AreEqual(1, newData.Length);

	        Assert.AreEqual(symbolOld, oldData[0].Get("symbol"));
	        Assert.AreEqual(volSumOld, oldData[0].Get("volSum"));

	        Assert.AreEqual(symbolNew, newData[0].Get("symbol"));
	        Assert.AreEqual(volSumNew, newData[0].Get("volSum"));

	        _testListener.Reset();
	        Assert.IsFalse(_testListener.IsInvoked);
	    }

	    private void SendEvent(string symbol, long volume)
	    {
	        var bean = new SupportMarketDataBean(symbol, 0, volume, null);
	        _epService.EPRuntime.SendEvent(bean);
	    }

        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
	}
} // end of namespace
