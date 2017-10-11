﻿using System;
using System.Collections;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using MyFlightbook;
using MySql.Data.MySqlClient;

/******************************************************
 * 
 * Copyright (c) 2012-2016 MyFlightbook LLC
 * Contact myflightbook-at-gmail.com for more information
 *
*******************************************************/

public partial class Controls_mfbDownload : System.Web.UI.UserControl, IDownloadableAsData
{
    /// <summary>
    /// The username to download.  Caller must validate authorization.
    /// </summary>
    public string User { get; set;}

    private const string szVSQuery = "viewStateQuery";

    public FlightQuery Restriction
    {
        get { return (FlightQuery)ViewState[szVSQuery] ?? new FlightQuery(User); }
        set { ViewState[szVSQuery] = value; }
    }

    /// <summary>
    /// Controls whether or not the option to download a PDF file is offered
    /// </summary>
    public Boolean OfferPDFDownload
    {
        get { return pnlDownloadPDF.Visible; }
        set { pnlDownloadPDF.Visible = value; }
    }

    /// <summary>
    /// Controls whether or not the option to view the whole grid is offered
    /// </summary>
    public Boolean ShowLogbookData
    {
        get { return gvFlightLogs.Visible; }
        set { gvFlightLogs.Visible = value; }
    }

    /// <summary>
    /// String specifying the desired order of columns
    /// </summary>
    public string OrderString { get; set; }

    protected string FormatTimeSpan(object o1, object o2)
    {
        if (!(o1 is DateTime && o2 is DateTime))
            return string.Empty;

        DateTime dt1 = (DateTime)o1;
        DateTime dt2 = (DateTime)o2;

        if (dt1.HasValue() && dt2.HasValue())
        {
            double cHours = dt2.Subtract(dt1).TotalHours;
            return (cHours > 0) ? String.Format(CultureInfo.CurrentCulture, "{0:#.##}", cHours) : string.Empty;
        }
        else
            return string.Empty;
    }

    public void UpdateData()
    {
        if (User.Length > 0)
        {
            using (MySqlCommand comm = new MySqlCommand())
            {
                DBHelper.InitCommandObject(comm, LogbookEntry.QueryCommand(Restriction));
                comm.CommandTimeout = 80; // use a longer timeout - this could be slow.  

                try
                {
                    using (MySqlDataAdapter da = new MySqlDataAdapter(comm))
                    {
                        using (DataSet dsFlights = new DataSet())
                        {
                            dsFlights.Locale = CultureInfo.CurrentCulture;
                            da.Fill(dsFlights);
                            gvFlightLogs.DataSource = dsFlights;

                            // Get the list of property types used by this user to create additional columns
                            comm.CommandText = "SELECT DISTINCT cpt.Title FROM custompropertytypes cpt INNER JOIN flightproperties fp ON fp.idPropType=cpt.idPropType INNER JOIN flights f ON f.idFlight=fp.idFlight WHERE f.username=?uName";
                            // parameters should still be valid

                            Hashtable htProps = new Hashtable(); // maps titles to the relevant column in the gridview
                            int cColumns = gvFlightLogs.Columns.Count;
                            using (DataSet dsProps = new DataSet())
                            {
                                dsProps.Locale = CultureInfo.CurrentCulture;
                                da.Fill(dsProps);

                                // add a new column for each property and store the column number in the hashtable (keyed by title)
                                foreach (DataRow dr in dsProps.Tables[0].Rows)
                                {
                                    htProps[dr.ItemArray[0]] = cColumns++;
                                    BoundField bf = new BoundField();
                                    bf.HeaderText = dr.ItemArray[0].ToString();
                                    bf.HtmlEncode = false;
                                    bf.DataField = "";
                                    bf.DataFormatString = "";
                                    gvFlightLogs.Columns.Add(bf);
                                }
                            }

                            if (OrderString != null && OrderString.Length > 0)
                            {
                                char[] delimit = { ',' };
                                string[] rgszCols = OrderString.Split(delimit);
                                ArrayList alCols = new ArrayList();

                                // identify the requested front columns
                                foreach (string szcol in rgszCols)
                                {
                                    int col = 0;
                                    if (int.TryParse(szcol, NumberStyles.Integer, CultureInfo.InvariantCulture, out col))
                                    {
                                        if (col < gvFlightLogs.Columns.Count)
                                            alCols.Add(col);
                                    }
                                }

                                int[] rgCols = (int[])alCols.ToArray(typeof(int));

                                // pull those columns to the left; this creates a duplicate column and shifts everything right by one...
                                int iCol = 0;
                                for (iCol = 0; iCol < rgCols.Length; iCol++)
                                    gvFlightLogs.Columns.Insert(iCol, gvFlightLogs.Columns[rgCols[iCol] + iCol]);

                                // And then remove the duplicates, from right to left
                                Array.Sort(rgCols);
                                for (int i = rgCols.Length - 1; i >= 0; i--)
                                    gvFlightLogs.Columns.RemoveAt(rgCols[i] + iCol);
                            }

                            gvFlightLogs.DataBind();

                            // now splice in all of the properties above
                            // ?localecode and ?shortDate are already in the parameters, from above.
                            comm.CommandText = "SELECT ELT(cpt.type + 1, cast(fdc.intValue as char), cast(FORMAT(fdc.decValue, 2, ?localecode) as char), if(fdc.intValue = 0, 'No', 'Yes'), DATE_FORMAT(fdc.DateValue, ?shortDate), cast(DateValue as char), StringValue, cast(fdc.decValue AS char))  AS PropVal, fdc.idFlight AS idFlight, cpt.title AS Title FROM flightproperties fdc INNER JOIN custompropertytypes cpt ON fdc.idPropType=cpt.idPropType INNER JOIN flights f ON f.idflight=fdc.idFlight WHERE username=?uName";

                            // and parameters should still be valid!
                            using (DataSet dsPropValues = new DataSet())
                            {
                                dsPropValues.Locale = CultureInfo.CurrentCulture;
                                da.Fill(dsPropValues);
                                foreach (GridViewRow gvr in gvFlightLogs.Rows)
                                {
                                    int idFlight = Convert.ToInt32(dsFlights.Tables[0].Rows[gvr.RowIndex]["idFlight"], CultureInfo.CurrentCulture);
                                    foreach (DataRow dr in dsPropValues.Tables[0].Rows)
                                    {
                                        if (idFlight == Convert.ToInt32(dr["idFlight"], CultureInfo.CurrentCulture))
                                            gvr.Cells[Convert.ToInt32(htProps[dr["Title"]], CultureInfo.CurrentCulture)].Text = dr["PropVal"].ToString();
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (comm.Connection != null && comm.Connection.State != ConnectionState.Closed)
                        comm.Connection.Close();
                }
            }
        }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            User = User ?? string.Empty;
            OrderString = OrderString ?? string.Empty;
        }
    }

    public string CSVData()
    {
        return gvFlightLogs.CSVFromData();
    }

    public void gvFlightLogs_RowDataBound(Object sender, GridViewRowEventArgs e)
    {
        if (e == null)
            throw new ArgumentNullException("e");
        if (e.Row.RowType == DataControlRowType.DataRow)
        {
            object props = DataBinder.Eval(e.Row.DataItem, "CustomProperties");
            object times = DataBinder.Eval(e.Row.DataItem, "timestamps");
            object tach = DataBinder.Eval(e.Row.DataItem, "tachtime");
            string szProperties = (props == null || props == System.DBNull.Value) ? string.Empty : (string) props;;
            string szTimeStamps = (times == null || times == System.DBNull.Value) ? string.Empty : (string) times;
            string szTach = (tach == null || tach == System.DBNull.Value) ? string.Empty : (string)tach;

            szProperties = LogbookEntry.AdjustCurrency(szProperties + LogbookEntryDisplay.PropertyTotals(szTimeStamps, szTach));

            if (szProperties.Length > 0)
            {
                PlaceHolder plcProperties = (PlaceHolder)e.Row.FindControl("plcProperties");
                TableCell tc = (TableCell) plcProperties.Parent;
                tc.Text = szProperties;
            }
        }
    }

    protected string CSVInUSCulture()
    {
        // ALWAYS download in US conventions
        CultureInfo ciSave = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        UpdateData();

        StringBuilder sbCSV = new StringBuilder();
        sbCSV.Append('\uFEFF'); // UTF-8 BOM
        sbCSV.Append(CSVData());
        System.Threading.Thread.CurrentThread.CurrentCulture = ciSave;

        return sbCSV.ToString();
    }

    #region obsolete download PDF
    /*
    /// <summary>
    /// Downloads a printable PDF version of the logbook.  ENDS THE CURRENT HTTP REQUEST!!
    /// </summary>
    /// <param name="fPostToOpenShift">true to post to a 3rd party server hosted on openshift (false requires python and tex to be set up and available on the server)</param>
    protected void DownloadPDFVersion(bool fPostToOpenShift = true)
    {
        // Download a CSV for the user and pass it to Till's tex writer.
        MyFlightbook.Profile pf = MyFlightbook.Profile.GetUser(User);

        StringBuilder sbLicense = new StringBuilder(pf.License);
        if (sbLicense.Length > 0 && !String.IsNullOrEmpty(pf.Certificate))
            sbLicense.AppendFormat(CultureInfo.CurrentCulture, ";{0}{1}", pf.Certificate, pf.CertificateExpiration.HasValue() ? String.Format(CultureInfo.CurrentCulture, " ({0})", pf.CertificateExpiration.ToShortDateString()) : string.Empty);

        string[] rgAddress = pf.Address.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        NameValueCollection postParams = new NameValueCollection()
               {
                   { "locale", "en_US" },
                   { fPostToOpenShift ? "pilot_name" : "pilotname", pf.UserFullName},
                   { "address1", rgAddress.Length > 0 ? rgAddress[0] : string.Empty},
                   { "address2", rgAddress.Length > 1 ? rgAddress[1] : string.Empty},
                   { "address3", rgAddress.Length > 2 ? rgAddress[2] : string.Empty},
                   { fPostToOpenShift ? "license_nr" : "license", sbLicense.ToString() }
               };

        if (pf.UsesUTCDateOfFlight)
            postParams.Add("utconly", "on");
        if (!pf.UsesHHMM)
            postParams.Add("fractions", "on");

        if (fPostToOpenShift)
        {
            const string szURL = "http://myflightbookpdf-tillgerken.rhcloud.com/compile";
            const string szDebugURL = "http://myflightbookpdfdev-tillgerken.rhcloud.com/compile";

            Uri uriTarget = new Uri(util.GetIntParam(Page.Request, "dev", 0) == 1 ? szDebugURL : szURL);

            using (MultipartFormDataContent form = new MultipartFormDataContent())
            {
                // Add each of the parameters
                foreach (string key in postParams.Keys)
                {
                    StringContent sc = new StringContent(postParams[key]);
                    form.Add(sc);
                    sc.Headers.ContentDisposition = (new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data") { Name = key });
                }


                // And the raw file
                string szCSV = CSVInUSCulture();
                if (String.IsNullOrEmpty(szCSV) || szCSV.Length == 1 && szCSV[0].CompareTo('\uFEFF') == 0)
                {
                    lblPDFErr.Text = Resources.LocalizedText.errPDFNoFlights;
                    return;
                }

                StringContent scContent = new StringContent(szCSV);
                form.Add(scContent);
                scContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
                scContent.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data") { Name = "csvfile", FileName = pf.UserName + ".csv" };

                using (HttpClient httpClient = new HttpClient())
                {
                    try
                    {
                        HttpResponseMessage response = httpClient.PostAsync(uriTarget, form).Result;
                        response.EnsureSuccessStatusCode();

                        Page.Response.Clear();
                        Page.Response.ContentType = response.Content.Headers.ContentType.ToString();
                        Response.AddHeader("content-disposition", String.Format(CultureInfo.CurrentCulture, @"attachment;filename=""{0}.pdf""", pf.UserFullName));
                        System.Threading.Tasks.Task.Run(async () => { await response.Content.CopyToAsync(Page.Response.OutputStream); }).Wait();
                        Page.Response.Flush();

                        // See http://stackoverflow.com/questions/20988445/how-to-avoid-response-end-thread-was-being-aborted-exception-during-the-exce for the reason for the next two lines.
                        Page.Response.SuppressContent = true;  // Gets or sets a value indicating whether to send HTTP content to the client.
                        HttpContext.Current.ApplicationInstance.CompleteRequest(); // Causes ASP.NET to bypass all events and filtering in the HTTP pipeline chain of execution and directly execute the EndRequest event.
                    }
                    catch (System.Threading.ThreadAbortException)
                    {
                    }
                    catch (System.Web.HttpUnhandledException ex)
                    {
                        lblPDFErr.Text = ex.Message;
                    }
                    catch (System.Web.HttpException ex)
                    {
                        lblPDFErr.Text = ex.Message;
                    }
                    catch (System.Net.WebException ex)
                    {
                        lblPDFErr.Text = ex.Message;
                    }
                }
            }
        }
        else
        {
            throw new NotImplementedException("Generating PDF on local server is not yet working");

            // Write the data to a temp file
            string szPath = Path.GetTempPath();
            string szFilenameBase = String.Format(CultureInfo.InvariantCulture, "{0}{1}", DateTime.Now.ToString("yyyyMMddhhmmssfff", CultureInfo.InvariantCulture), pf.UserName);
            string szBaseFile = szPath + szFilenameBase;
            string szCSVPath = szBaseFile + ".csv";
            string szTexPath = szBaseFile + ".tex";
            string szPdfPath = szBaseFile + ".pdf";

            try
            {
                File.WriteAllBytes(szCSVPath, Encoding.UTF8.GetBytes(sbCSV.ToString()));

                string szTexBatPath = LocalConfig.SettingForKey("csvtopdfpath");

                // create the .tex file
                ProcessStartInfo psiPython = new ProcessStartInfo(szTexBatPath, String.Format(CultureInfo.InvariantCulture, "{0} {1} {2} \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\"",
                    szPath,
                    szFilenameBase,
                    System.Globalization.RegionInfo.CurrentRegion.ThreeLetterISORegionName.ToLower(),
                    postParams["pilotname"],
                    postParams["address1"],
                    postParams["address2"],
                    postParams["address3"],
                    postParams["license"]
                    ))
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                string szBatchoutput = string.Empty;
                string szBatchError = string.Empty;

                //Create the .TEX file
                using (Process p = new Process() { StartInfo = psiPython })
                {
                    p.Start();
                    szBatchoutput = p.StandardOutput.ReadToEnd();
                    szBatchError = p.StandardError.ReadToEnd();
                    p.WaitForExit(20000);   // wait up to 20 seconds
                }

                if (!File.Exists(szTexPath))
                    throw new MyFlightbookException("Conversion from CSV to TeX failed: " + szBatchError);

                if (File.Exists(szPdfPath))
                {
                    Response.Clear();
                    Response.ContentType = "application/pdf";
                    string szDisposition = String.Format(CultureInfo.InvariantCulture, "inline;filename={0}.csv", System.Text.RegularExpressions.Regex.Replace(szPdfPath, "[^0-9a-zA-Z-]", ""));
                    Response.AddHeader("Content-Disposition", szDisposition);

                    Response.WriteFile(szPdfPath);
                    Response.Flush();
                    Response.End();
                }
                else
                    throw new MyFlightbookException("PDF file creation failed.");

            }
            catch (System.InvalidOperationException ex)
            {
                lblPDFErr.Text = ex.Message;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                lblPDFErr.Text = ex.Message;
            }
            catch (MyFlightbookException ex)
            {
                lblPDFErr.Text = ex.Message;
            }
            finally
            {
                // Clean up and return
                DirectoryInfo dir = new DirectoryInfo(szPath);
                FileInfo[] rgFiles = dir.GetFiles(szFilenameBase + ".*");

                foreach (FileInfo fi in rgFiles)
                    fi.Delete();
            }
        }
    }

    protected void lnkDownloadPDF_Click(object sender, EventArgs e)
    {
        if (String.IsNullOrEmpty(User))
            User = Page.User.Identity.Name;
        DownloadPDFVersion();
    }
    */
    #endregion

    public byte[] RawData(string szUser)
    {
        if (String.IsNullOrEmpty(szUser))
            return new byte[0];
        else
        {
            User = szUser;
            UpdateData();
            UTF8Encoding enc = new UTF8Encoding(true);    // to include the BOM
            byte[] preamble = enc.GetPreamble();
            string body = CSVData();
            byte[] allBytes = new byte[preamble.Length + enc.GetByteCount(body)]; ;
            for (int i = 0; i < preamble.Length; i++)
                allBytes[i] = preamble[i];
            enc.GetBytes(body, 0, body.Length, allBytes, preamble.Length);
            return allBytes;
        }
    }
}
