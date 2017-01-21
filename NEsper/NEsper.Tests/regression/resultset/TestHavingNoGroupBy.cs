///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using com.espertech.esper.client;
using com.espertech.esper.client.scopetest;
using com.espertech.esper.client.soda;
using com.espertech.esper.compat.logging;
using com.espertech.esper.metrics.instrumentation;
using com.espertech.esper.support.bean;
using com.espertech.esper.support.client;
using com.espertech.esper.util;

using NUnit.Framework;

namespace com.espertech.esper.regression.resultset
{
    [TestFixture]
	public class TestHavingNoGroupBy 
	{
        private const string SYMBOL_DELL = "DELL";

        private EPServiceProvider _epService;
	    private SupportUpdateListener _listener;

        [SetUp]
	    public void SetUp()
	    {
	        _listener = new SupportUpdateListener();
	        _epService = EPServiceProviderManager.GetDefaultProvider(SupportConfigFactory.GetConfiguration());
	        _epService.Initialize();
	        if (InstrumentationHelper.ENABLED) { InstrumentationHelper.StartTest(_epService, this.GetType(), this.GetType().FullName);}
	    }

        [TearDown]
	    public void TearDown()
        {
	        if (InstrumentationHelper.ENABLED) { InstrumentationHelper.EndTest();}
	        _listener = null;
	    }

        [Test]
	    public void TestHavingWildcardSelect()
        {
	        _epService.EPAdministrator.Configuration.AddEventType<SupportBean>();
	        var epl = "select * " +
	                "from SupportBean.win:length_batch(2) " +
	                "where IntPrimitive>0 " +
	                "having count(*)=2";
	        var stmt = _epService.EPAdministrator.CreateEPL(epl);
	        stmt.AddListener(_listener);

	        _epService.EPRuntime.SendEvent(new SupportBean("E1", 0));
	        _epService.EPRuntime.SendEvent(new SupportBean("E2", 0));
	        Assert.IsFalse(_listener.IsInvoked);

	        _epService.EPRuntime.SendEvent(new SupportBean("E3", 1));
	        _epService.EPRuntime.SendEvent(new SupportBean("E4", 1));
	        Assert.IsTrue(_listener.GetAndClearIsInvoked());

	        _epService.EPRuntime.SendEvent(new SupportBean("E3", 0));
	        _epService.EPRuntime.SendEvent(new SupportBean("E4", 1));
	        Assert.IsFalse(_listener.GetAndClearIsInvoked());
	    }

        [Test]
	    public void TestSumOneViewOM()
	    {
	        var model = new EPStatementObjectModel();
	        model.SelectClause = SelectClause.Create("Symbol", "Price").SetStreamSelector(StreamSelector.RSTREAM_ISTREAM_BOTH).Add(Expressions.Avg("Price"), "avgPrice");
	        model.FromClause = FromClause.Create(FilterStream.Create(typeof(SupportMarketDataBean).FullName).AddView("win", "length", Expressions.Constant(5)));
	        model.HavingClause = Expressions.Lt(Expressions.Property("Price"), Expressions.Avg("Price"));
	        model = (EPStatementObjectModel) SerializableObjectCopier.Copy(model);

	        var viewExpr = "select irstream Symbol, Price, avg(Price) as avgPrice " +
	                          "from " + typeof(SupportMarketDataBean).FullName + ".win:length(5) " +
	                          "having Price<avg(Price)";
	        Assert.AreEqual(viewExpr, model.ToEPL());

	        var selectTestView = _epService.EPAdministrator.Create(model);
	        selectTestView.AddListener(_listener);

	        RunAssertion(selectTestView);
	    }

        [Test]
	    public void TestSumOneView()
	    {
	        var viewExpr = "select irstream Symbol, Price, avg(Price) as avgPrice " +
	                          "from " + typeof(SupportMarketDataBean).FullName + ".win:length(5) " +
	                          "having Price < avg(Price)";

	        var selectTestView = _epService.EPAdministrator.CreateEPL(viewExpr);
	        selectTestView.AddListener(_listener);

	        RunAssertion(selectTestView);
	    }

        [Test]
	    public void TestSumJoin()
	    {
	        var viewExpr = "select irstream Symbol, Price, avg(Price) as avgPrice " +
	                          "from " + typeof(SupportBeanString).FullName + ".win:length(100) as one, " +
	                                    typeof(SupportMarketDataBean).FullName + ".win:length(5) as two " +
	                          "where one.TheString = two.Symbol " +
	                          "having Price < avg(Price)";

	        var selectTestView = _epService.EPAdministrator.CreateEPL(viewExpr);
	        selectTestView.AddListener(_listener);

	        _epService.EPRuntime.SendEvent(new SupportBeanString(SYMBOL_DELL));

	        RunAssertion(selectTestView);
	    }

        [Test]
	    public void TestSumHavingNoAggregatedProp()
	    {
	        var viewExpr = "select irstream Symbol, Price, avg(Price) as avgPrice " +
	                          "from " + typeof(SupportMarketDataBean).FullName + ".win:length(5) as two " +
	                          "having Volume < avg(Price)";

	        var selectTestView = _epService.EPAdministrator.CreateEPL(viewExpr);
	        selectTestView.AddListener(_listener);
	    }

        [Test]
	    public void TestNoAggregationJoinHaving()
	    {
	        RunNoAggregationJoin("having");
	    }

        [Test]
	    public void TestNoAggregationJoinWhere()
	    {
	        RunNoAggregationJoin("where");
	    }

        [Test]
	    public void TestSubstreamSelectHaving()
	    {
	        _epService.EPAdministrator.Configuration.AddEventType<SupportBean>();
	        var stmtText = "insert into MyStream select quote.* from SupportBean.win:length(14) quote having avg(IntPrimitive) >= 3\n";
	        var stmt = _epService.EPAdministrator.CreateEPL(stmtText);
	        stmt.AddListener(_listener);

	        _epService.EPRuntime.SendEvent(new SupportBean("abc", 2));
	        Assert.IsFalse(_listener.IsInvoked);
	        _epService.EPRuntime.SendEvent(new SupportBean("abc", 2));
	        Assert.IsFalse(_listener.IsInvoked);
	        _epService.EPRuntime.SendEvent(new SupportBean("abc", 3));
	        Assert.IsFalse(_listener.IsInvoked);
	        _epService.EPRuntime.SendEvent(new SupportBean("abc", 5));
	        Assert.IsTrue(_listener.IsInvoked);
	    }

	    private void RunNoAggregationJoin(string filterClause)
	    {
	        var viewExpr = "select irstream a.Price as aPrice, b.Price as bPrice, Math.Max(a.Price, b.Price) - Math.Min(a.Price, b.Price) as spread " +
	                          "from " + typeof(SupportMarketDataBean).FullName + "(Symbol='SYM1').win:length(1) as a, " +
	                                    typeof(SupportMarketDataBean).FullName + "(Symbol='SYM2').win:length(1) as b " +
	                          filterClause + " Math.Max(a.Price, b.Price) - Math.Min(a.Price, b.Price) >= 1.4";

	        var selectTestView = _epService.EPAdministrator.CreateEPL(viewExpr);
	        selectTestView.AddListener(_listener);

	        SendPriceEvent("SYM1", 20);
	        Assert.IsFalse(_listener.IsInvoked);

	        SendPriceEvent("SYM2", 10);
	        AssertNewSpreadEvent(20, 10, 10);

	        SendPriceEvent("SYM2", 20);
	        AssertOldSpreadEvent(20, 10, 10);

	        SendPriceEvent("SYM2", 20);
	        SendPriceEvent("SYM2", 20);
	        SendPriceEvent("SYM1", 20);
	        Assert.IsFalse(_listener.IsInvoked);

	        SendPriceEvent("SYM1", 18.7);
	        Assert.IsFalse(_listener.IsInvoked);

	        SendPriceEvent("SYM2", 20);
	        Assert.IsFalse(_listener.IsInvoked);

	        SendPriceEvent("SYM1", 18.5);
	        AssertNewSpreadEvent(18.5, 20, 1.5d);

	        SendPriceEvent("SYM2", 16);
	        AssertOldNewSpreadEvent(18.5, 20, 1.5d, 18.5, 16, 2.5d);

	        SendPriceEvent("SYM1", 12);
	        AssertOldNewSpreadEvent(18.5, 16, 2.5d, 12, 16, 4);
	    }

	    private void AssertOldNewSpreadEvent(double oldaPrice, double oldbPrice, double oldspread,
	                                         double newaPrice, double newbPrice, double newspread)
	    {
	        Assert.AreEqual(1, _listener.OldDataList.Count);
	        Assert.AreEqual(1, _listener.LastOldData.Length);
	        Assert.AreEqual(1, _listener.NewDataList.Count);   // since event null is put into the list
	        Assert.AreEqual(1, _listener.LastNewData.Length);

	        var oldEvent = _listener.LastOldData[0];
	        var newEvent = _listener.LastNewData[0];

	        CompareSpreadEvent(oldEvent, oldaPrice, oldbPrice, oldspread);
	        CompareSpreadEvent(newEvent, newaPrice, newbPrice, newspread);

	        _listener.Reset();
	    }

	    private void AssertOldSpreadEvent(double aPrice, double bPrice, double spread)
	    {
	        Assert.AreEqual(1, _listener.OldDataList.Count);
	        Assert.AreEqual(1, _listener.LastOldData.Length);
	        Assert.AreEqual(1, _listener.NewDataList.Count);   // since event null is put into the list
	        Assert.IsNull(_listener.LastNewData);

	        var theEvent = _listener.LastOldData[0];

	        CompareSpreadEvent(theEvent, aPrice, bPrice, spread);
	        _listener.Reset();
	    }

	    private void AssertNewSpreadEvent(double aPrice, double bPrice, double spread)
	    {
	        Assert.AreEqual(1, _listener.NewDataList.Count);
	        Assert.AreEqual(1, _listener.LastNewData.Length);
	        Assert.AreEqual(1, _listener.OldDataList.Count);
	        Assert.IsNull(_listener.LastOldData);

	        var theEvent = _listener.LastNewData[0];
	        CompareSpreadEvent(theEvent, aPrice, bPrice, spread);
	        _listener.Reset();
	    }

	    private void CompareSpreadEvent(EventBean theEvent, double aPrice, double bPrice, double spread)
	    {
	        Assert.AreEqual(aPrice, theEvent.Get("aPrice"));
	        Assert.AreEqual(bPrice, theEvent.Get("bPrice"));
	        Assert.AreEqual(spread, theEvent.Get("spread"));
	    }

	    private void SendPriceEvent(string symbol, double price)
	    {
	        _epService.EPRuntime.SendEvent(new SupportMarketDataBean(symbol, price, -1L, null));
	    }

	    private void RunAssertion(EPStatement selectTestView)
	    {
	        // assert select result type
	        Assert.AreEqual(typeof(string), selectTestView.EventType.GetPropertyType("Symbol"));
	        Assert.AreEqual(typeof(double?), selectTestView.EventType.GetPropertyType("Price"));
	        Assert.AreEqual(typeof(double?), selectTestView.EventType.GetPropertyType("avgPrice"));

	        SendEvent(SYMBOL_DELL, 10);
	        Assert.IsFalse(_listener.IsInvoked);

	        SendEvent(SYMBOL_DELL, 5);
	        AssertNewEvents(SYMBOL_DELL, 5d, 7.5d);

	        SendEvent(SYMBOL_DELL, 15);
	        Assert.IsFalse(_listener.IsInvoked);

	        SendEvent(SYMBOL_DELL, 8);  // avg = (10 + 5 + 15 + 8) / 4 = 38/4=9.5
	        AssertNewEvents(SYMBOL_DELL, 8d, 9.5d);

	        SendEvent(SYMBOL_DELL, 10);  // avg = (10 + 5 + 15 + 8 + 10) / 5 = 48/5=9.5
	        Assert.IsFalse(_listener.IsInvoked);

	        SendEvent(SYMBOL_DELL, 6);  // avg = (5 + 15 + 8 + 10 + 6) / 5 = 44/5=8.8
	        // no old event posted, old event falls above current avg Price
	        AssertNewEvents(SYMBOL_DELL, 6d, 8.8d);

	        SendEvent(SYMBOL_DELL, 12);  // avg = (15 + 8 + 10 + 6 + 12) / 5 = 51/5=10.2
	        AssertOldEvents(SYMBOL_DELL, 5d, 10.2d);
	    }

        [Test]
	    public void TestHavingSum()
	    {
	        var stmt = "select irstream sum(myEvent.IntPrimitive) as mysum from pattern [every myEvent=" + typeof(SupportBean).FullName +
	                "] having sum(myEvent.IntPrimitive) = 2";
	        var selectTestView = _epService.EPAdministrator.CreateEPL(stmt);
	        selectTestView.AddListener(_listener);

	        SendEvent(1);
	        Assert.IsFalse(_listener.IsInvoked);

	        SendEvent(1);
	        Assert.AreEqual(2, _listener.AssertOneGetNewAndReset().Get("mysum"));

	        SendEvent(1);
	        Assert.AreEqual(2, _listener.AssertOneGetOldAndReset().Get("mysum"));
	    }

        [Test]
	    public void TestHavingSumIStream()
	    {
	        var stmt = "select istream sum(myEvent.IntPrimitive) as mysum from pattern [every myEvent=" + typeof(SupportBean).FullName +
	                "] having sum(myEvent.IntPrimitive) = 2";
	        var selectTestView = _epService.EPAdministrator.CreateEPL(stmt);
	        selectTestView.AddListener(_listener);

	        SendEvent(1);
	        Assert.IsFalse(_listener.IsInvoked);

	        SendEvent(1);
	        Assert.AreEqual(2, _listener.AssertOneGetNewAndReset().Get("mysum"));

	        SendEvent(1);
	        Assert.IsFalse(_listener.IsInvoked);
	    }

	    private void AssertNewEvents(string symbol, double? newPrice, double? newAvgPrice)
	    {
	        var oldData = _listener.LastOldData;
	        var newData = _listener.LastNewData;

	        Assert.IsNull(oldData);
	        Assert.AreEqual(1, newData.Length);

	        Assert.AreEqual(symbol, newData[0].Get("Symbol"));
	        Assert.AreEqual(newPrice, newData[0].Get("Price"));
	        Assert.AreEqual(newAvgPrice, newData[0].Get("avgPrice"));

	        _listener.Reset();
	    }

        private void AssertOldEvents(string symbol, double? oldPrice, double? oldAvgPrice)
	    {
	        var oldData = _listener.LastOldData;
	        var newData = _listener.LastNewData;

	        Assert.IsNull(newData);
	        Assert.AreEqual(1, oldData.Length);

	        Assert.AreEqual(symbol, oldData[0].Get("Symbol"));
	        Assert.AreEqual(oldPrice, oldData[0].Get("Price"));
	        Assert.AreEqual(oldAvgPrice, oldData[0].Get("avgPrice"));

	        _listener.Reset();
	    }

	    private void SendEvent(int intPrimitive)
	    {
	        var bean = new SupportBean();
	        bean.IntPrimitive = intPrimitive;
	        _epService.EPRuntime.SendEvent(bean);
	    }

	    private void SendEvent(string symbol, double price)
	    {
	        var bean = new SupportMarketDataBean(symbol, price, 0L, null);
	        _epService.EPRuntime.SendEvent(bean);
	    }

        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
	}
} // end of namespace
