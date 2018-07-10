﻿<%@ Page Language="C#" AutoEventWireup="true" MasterPageFile="~/MasterPage.master" CodeFile="MergeTelemetry.aspx.cs" Inherits="PlayPen_MergeTelemetry" %>
<%@ MasterType VirtualPath="~/MasterPage.master" %>
<%@ Register Assembly="AjaxControlToolkit" Namespace="AjaxControlToolkit" TagPrefix="asp" %>

<asp:Content ID="Content2" ContentPlaceHolderID="cpPageTitle" Runat="Server">
    Merge Telemetry
</asp:Content>
<asp:Content ID="Content1" ContentPlaceHolderID="cpMain" runat="Server">
    <p>Upload the files to merge:</p>

    <asp:AjaxFileUpload ID="AjaxFileUpload1" runat="server" CssClass="mfbDefault"
            ThrobberID="myThrobber" MaximumNumberOfFiles="20" OnUploadComplete="AjaxFileUpload1_UploadComplete" />
    <asp:Image ID="myThrobber" ImageUrl="~/images/ajax-loader.gif" runat="server" style="display:None" />
    <asp:Label ID="lblErr" runat="server" CssClass="error" EnableViewState="false"></asp:Label>
    <asp:Button ID="btnMerge" runat="server" Text="Merge to GPX" OnClick="btnMerge_Click" />
    <asp:HiddenField ID="hdnHasTime" runat="server" Value="true" />
    <asp:HiddenField ID="hdnHasAlt" runat="server" Value="true" />
    <asp:HiddenField ID="hdnHasSpeed" runat="server" Value="true" />
    <asp:HiddenField ID="hdnGUID" runat="server" />
</asp:Content>

