﻿using MyFlightbook;
using MyFlightbook.Printing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Web.UI.WebControls;

/******************************************************
 * 
 * Copyright (c) 2018-2020 MyFlightbook LLC
 * Contact myflightbook-at-gmail.com for more information
 *
*******************************************************/


public partial class Controls_PrintingLayouts_layoutPortrait : System.Web.UI.UserControl, IPrintingTemplate
{
    public MyFlightbook.Profile CurrentUser { get; set; }

    public bool IncludeImages { get; set; }

    protected bool ShowFooter { get; set; }

    protected Collection<OptionalColumn> OptionalColumns { get; private set; }

    protected Boolean ShowOptionalColumn(int index)
    {
        return OptionalColumns != null && index >= 0 && index < OptionalColumns.Count;
    }

    protected string OptionalColumnName(int index)
    {
        return ShowOptionalColumn(index) ? OptionalColumns[index].Title : string.Empty;
    }

    protected int ColumnCount
    {
        get { return Math.Min(OptionalColumns.Count, 4) + 17; }
    }

    #region IPrintingTemplate
    public void BindPages(IEnumerable<LogbookPrintedPage> lst, Profile user, PrintingOptions options, bool showFooter = true)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        ShowFooter = showFooter;
        IncludeImages = options.IncludeImages;
        CurrentUser = user;
        OptionalColumns = options.OptionalColumns;

        rptPages.DataSource = lst;
        rptPages.DataBind();
    }
    #endregion

    protected void Page_Load(object sender, EventArgs e) { CurrentUser = MyFlightbook.Profile.GetUser(Page.User.Identity.Name); }

    protected void rptPages_ItemDataBound(object sender, RepeaterItemEventArgs e)
    {
        if (e == null)
            throw new ArgumentNullException(nameof(e));

        LogbookPrintedPage lep = (LogbookPrintedPage)e.Item.DataItem;

        Repeater rpt = (Repeater)e.Item.FindControl("rptFlight");
        rpt.DataSource = lep.Flights;
        rpt.DataBind();

        rpt = (Repeater)e.Item.FindControl("rptSubtotalCollections");
        rpt.DataSource = lep.Subtotals;
        rpt.DataBind();
    }

    protected void rptFlight_ItemDataBound(object sender, RepeaterItemEventArgs e)
    {
        if (e == null)
            throw new ArgumentNullException(nameof(e));

        LogbookEntryDisplay led = (LogbookEntryDisplay)e.Item.DataItem;
        Controls_mfbImageList mfbil = (Controls_mfbImageList)e.Item.FindControl("mfbilFlights");
        mfbil.Key = led.FlightID.ToString(CultureInfo.InvariantCulture);
        mfbil.Refresh(true, false);
        Controls_mfbSignature sig = (Controls_mfbSignature)e.Item.FindControl("mfbSignature");
        sig.Flight = led;
    }

    protected void rptSubtotalCollections_ItemDataBound(object sender, RepeaterItemEventArgs e)
    {
        if (e == null)
            throw new ArgumentNullException(nameof(e));

        LogbookPrintedPageSubtotalsCollection sc = (LogbookPrintedPageSubtotalsCollection)e.Item.DataItem;
        Repeater rpt = (Repeater)e.Item.FindControl("rptSubtotals");
        rpt.DataSource = sc.Subtotals;
        rpt.DataBind();
    }
}