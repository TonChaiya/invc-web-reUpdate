<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>
<%
' *** Logout the current user.
MM_Logout = CStr(Request.ServerVariables("URL")) & "?MM_Logoutnow=1"
If (CStr(Request("MM_Logoutnow")) = "1") Then
  Session.Contents.Remove("MM_Username")
  Session.Contents.Remove("MM_UserAuthorization")
  MM_logoutRedirectPage = "default.asp"
  ' redirect with URL parameters (remove the "MM_Logoutnow" query param).
  if (MM_logoutRedirectPage = "") Then MM_logoutRedirectPage = CStr(Request.ServerVariables("URL"))
  If (InStr(1, UC_redirectPage, "?", vbTextCompare) = 0 And Request.QueryString <> "") Then
    MM_newQS = "?"
    For Each Item In Request.QueryString
      If (Item <> "MM_Logoutnow") Then
        If (Len(MM_newQS) > 1) Then MM_newQS = MM_newQS & "&"
        MM_newQS = MM_newQS & Item & "=" & Server.URLencode(Request.QueryString(Item))
      End If
    Next
    if (Len(MM_newQS) > 1) Then MM_logoutRedirectPage = MM_logoutRedirectPage & MM_newQS
  End If
  Response.Redirect(MM_logoutRedirectPage)
End If
%>
<!--#include file="Connections/INVFlood.asp" -->
<%
Dim Drug
Dim Drug_numRows


Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.ActiveConnection = MM_INVFlood_STRING
if request.QueryString("flag") ="1" then
Drug.Source ="SELECT POMonitor.PONO, POMonitor.PODate, InvMonitor.InvoiceNo AS MaxOfInvoiceNo, InvMonitor.InvoiceDate AS LastOfInvoiceDate, PODetail.Wcode, DRUG.DRUG_NAME, PODetail.POAmount, [rec_no]/[rec_package] AS Rev_No, InvMonitorDetail.rec_package, PODetail.POUnit FROM DRUG RIGHT JOIN (InvMonitorDetail RIGHT JOIN (InvMonitor RIGHT JOIN (PODetail RIGHT JOIN POMonitor ON PODetail.PONO = POMonitor.PONO) ON InvMonitor.PONO = POMonitor.PONO) ON InvMonitorDetail.InvoiceNo = InvMonitor.InvoiceNo) ON DRUG.WORKING_CODE = PODetail.Wcode WHERE (((InvMonitor.InvoiceNo) Is Null));"
elseif request.QueryString("flag") ="2" then
Drug.Source = "SELECT PODetail.PONO, Max(InvMonitor.InvoiceNo) AS MaxOfInvoiceNo, POMonitor.PODate, Last(InvMonitor.InvoiceDate) AS LastOfInvoiceDate, PODetail.Wcode, DRUG.DRUG_NAME, PODetail.POAmount, Sum([rec_no]/[rec_package]) AS Rev_No, InvMonitorDetail.rec_package, PODetail.POUnit FROM DRUG INNER JOIN (InvMonitorDetail INNER JOIN (InvMonitor INNER JOIN (PODetail INNER JOIN POMonitor ON PODetail.PONO = POMonitor.PONO) ON InvMonitor.PONO = POMonitor.PONO) ON (InvMonitorDetail.Wcode = PODetail.Wcode) AND (InvMonitorDetail.InvoiceNo = InvMonitor.InvoiceNo)) ON DRUG.WORKING_CODE = PODetail.Wcode GROUP BY PODetail.PONO, POMonitor.PODate, PODetail.Wcode, DRUG.DRUG_NAME, PODetail.POAmount, InvMonitorDetail.rec_package, PODetail.POUnit HAVING (((Sum([rec_no]/[rec_package]))<>[PODetail].[poamount]));"
elseif request.QueryString("flag") ="3" then
Drug.Source = "SELECT PODetail.PONO, Max(InvMonitor.InvoiceNo) AS MaxOfInvoiceNo, POMonitor.PODate, Last(InvMonitor.InvoiceDate) AS LastOfInvoiceDate, PODetail.Wcode, DRUG.DRUG_NAME, PODetail.POAmount, Sum([rec_no]/[rec_package]) AS Rev_No, InvMonitorDetail.rec_package, PODetail.POUnit FROM DRUG INNER JOIN (InvMonitorDetail INNER JOIN (InvMonitor INNER JOIN (PODetail INNER JOIN POMonitor ON PODetail.PONO = POMonitor.PONO) ON InvMonitor.PONO = POMonitor.PONO) ON (InvMonitorDetail.Wcode = PODetail.Wcode) AND (InvMonitorDetail.InvoiceNo = InvMonitor.InvoiceNo)) ON DRUG.WORKING_CODE = PODetail.Wcode GROUP BY PODetail.PONO, POMonitor.PODate, PODetail.Wcode, DRUG.DRUG_NAME, PODetail.POAmount, InvMonitorDetail.rec_package, PODetail.POUnit HAVING (((Sum([rec_no]/[rec_package]))=[PODetail].[poamount]));"
else
Drug.Source ="SELECT POMonitor.PONO, POMonitor.PODate, InvMonitor.InvoiceNo AS MaxOfInvoiceNo, InvMonitor.InvoiceDate AS LastOfInvoiceDate, PODetail.Wcode, DRUG.DRUG_NAME, PODetail.POAmount, [rec_no]/[rec_package] AS Rev_No, InvMonitorDetail.rec_package, PODetail.POUnit FROM DRUG RIGHT JOIN (InvMonitorDetail RIGHT JOIN (InvMonitor RIGHT JOIN (PODetail RIGHT JOIN POMonitor ON PODetail.PONO = POMonitor.PONO) ON InvMonitor.PONO = POMonitor.PONO) ON InvMonitorDetail.InvoiceNo = InvMonitor.InvoiceNo) ON DRUG.WORKING_CODE = PODetail.Wcode WHERE (((InvMonitor.InvoiceNo) Is Null));"
end if 
Drug.CursorType = 3
Drug.CursorLocation = 3
Drug.LockType = 1
Drug.Open()

Drug_numRows = 0
%>
<%
Dim updateddate
Dim updateddate_numRows

Set updateddate = Server.CreateObject("ADODB.Recordset")
updateddate.ActiveConnection = MM_INVFlood_STRING
updateddate.Source = "SELECT *  FROM DataUpdated"
updateddate.CursorType = 0
updateddate.CursorLocation = 2
updateddate.LockType = 1
updateddate.Open()

updateddate_numRows = 0
%>
<%
Dim Repeat1__numRows
Dim Repeat1__index

Repeat1__numRows = -1
Repeat1__index = 0
Drug_numRows = Drug_numRows + Repeat1__numRows
%>
<%
' *** Validate request to log in to this site.
MM_LoginAction = Request.ServerVariables("URL")
If Request.QueryString<>"" Then MM_LoginAction = MM_LoginAction + "?" + Server.HTMLEncode(Request.QueryString)
MM_valUsername=CStr(Request.Form("textfield"))
If MM_valUsername <> "" Then
  MM_fldUserAuthorization=""
  MM_redirectLoginSuccess="default.asp"
  MM_redirectLoginFailed="default.asp"
  MM_flag="ADODB.Recordset"
  set MM_rsUser = Server.CreateObject(MM_flag)
  MM_rsUser.ActiveConnection = MM_INVFlood_STRING
  MM_rsUser.Source = "SELECT password, password"
  If MM_fldUserAuthorization <> "" Then MM_rsUser.Source = MM_rsUser.Source & "," & MM_fldUserAuthorization
  MM_rsUser.Source = MM_rsUser.Source & " FROM DataUpdated WHERE password='" & Replace(MM_valUsername,"'","''") &"' AND password='" & Replace(Request.Form("textfield"),"'","''") & "'"
  MM_rsUser.CursorType = 0
  MM_rsUser.CursorLocation = 2
  MM_rsUser.LockType = 3
  MM_rsUser.Open
  If Not MM_rsUser.EOF Or Not MM_rsUser.BOF Then 
    ' username and password match - this is a valid user
    Session("MM_Username") = MM_valUsername
    If (MM_fldUserAuthorization <> "") Then
      Session("MM_UserAuthorization") = CStr(MM_rsUser.Fields.Item(MM_fldUserAuthorization).Value)
    Else
      Session("MM_UserAuthorization") = ""
    End If
    if CStr(Request.QueryString("accessdenied")) <> "" And false Then
      MM_redirectLoginSuccess = Request.QueryString("accessdenied")
    End If
    MM_rsUser.Close
    Response.Redirect(MM_redirectLoginSuccess)
  End If
  MM_rsUser.Close
  Response.Redirect(MM_redirectLoginFailed)
End If
%>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<title>ระบบรายงานปริมาณยาและเวชภัณฑ์คงคลังเพื่อรองรับสถานการณ์น้ำท่วม 2554</title>
<style type="text/css">
<!--
.style1 {
	font-size: 24px;
	color: #0000FF;
}
.style6 {color: #0000FF}
.style7 {color: #FF0000}
.style8 {color: #FF99FF}
.style9 {color: #99FFFF}
.style10 {color: #66FF66}
-->
</style>
<!--#include file="menu.asp" -->
</head>

<body>
<%if Session("MM_Username") = "" then%>

<form id="form3" name="form3" form action="<%=MM_LoginAction%>" method="POST">
  <table width="20%" border="0" align="right" cellpadding="0" cellspacing="0">

    <tr>
      <td width="23%">Login:</td>
      <td width="77%"><label>
        <input name="textfield" type="password" size="10" />
        <input type="submit" name="Submit2" value="OK" />
      </label></td>
    </tr>
<%  else%>	
    <tr>
      <td width="90%"><div align="right"><a href="<%= MM_Logout %>">Logout</a></div></td>
    </tr>
<%end if%>
  </table>
</form>
<div align="right">


<br>
<br>
</div>
<table width="90%" border="0" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td colspan="2"><div align="center" class="style1">&#3619;&#3632;&#3610;&#3610;&#3619;&#3634;&#3618;&#3591;&#3634;&#3609;&#3611;&#3619;&#3636;&#3617;&#3634;&#3603;&#3618;&#3634;&#3649;&#3621;&#3632;&#3648;&#3623;&#3594;&#3616;&#3633;&#3603;&#3601;&#3660;&#3588;&#3591;ค&#3621;&#3633;&#3591;</div></td>
  </tr>
  <tr>
    <td colspan="2"><div align="center">วันที่ปรับปรุงข้อมูล<span class="style7"><%=(updateddate.Fields.Item("updatedate").Value)%></span></div></td>
  </tr>
</table>
<div align="center">
  <p><a href="default_print.asp"></a><br>
    <a href="postatus.asp?flag=1">ใบสั่งซื้อที่ยังไม่ได้รับของ</a> | <a href="postatus.asp?flag=2">รับของแล้วแต่ไม่ครบจำนวน</a> | <a href="postatus.asp?flag=3">รับของครบแล้วรอส่งเอกสาร</a></p>
</p>
  <p><%if request.QueryString("flag") ="1" then
  		response.write "ใบสั่งซื้อที่ยังไม่ได้รับของ"
		elseif request.QueryString("flag") ="2" then
		response.write "ใบสั่งซื้อที่รับของแล้วแต่ยังไม่ครบจำนวน"
		elseif request.QueryString("flag") ="3" then
		response.write "ใบสั่งซื้อที่รับของครบแล้วรอส่งเอกสาร"
		end if   %></p>
</div>
<table width="95%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="3%" bgcolor="#FFFF66"><div align="center">No. </div></td>
	<td width="8%" bgcolor="#FFFF66"><div align="center">PO No. </div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">PO date </div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">รหัสยา</div></td>
    <td width="30%" bgcolor="#FFFF66"><div align="center">ชื่อยา </div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">จำนวนสั่ง</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">Invoice No. </div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">Invoice date</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">จำนวนรับ</div></td>
  </tr>
  <%  runno = 1
While ((Repeat1__numRows <> 0) AND (NOT Drug.EOF)) 
%>
<tr> 


    <td><div align="center">
        <%response.write(runno)%>
    .  </div></td>
	<td><div align="center"><%=(Drug.Fields.Item("PONO").Value)%></div></td>
      <td><div align="center"><%=(Drug.Fields.Item("PODate").Value)%></div></td>
    <td ><div align="center"><%=(Drug.Fields.Item("Wcode").Value)%></div></td>
    <td ><div align="left"><%=(Drug.Fields.Item("DRUG_NAME").Value)%></div></td>
	    <td><div align="center">
          <%=(Drug.Fields.Item("POAmount").Value)%><%=(Drug.Fields.Item("POUnit").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("MaxOfInvoiceNo").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("LastOfInvoiceDate").Value)%></div></td>
<td>   <div align="center"><%=(Drug.Fields.Item("Rev_No").Value)%></div>
  <tr>
   
<%
  Repeat1__index=Repeat1__index+1
at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1
  Drug.MoveNext()
Wend
%>
</table>

</body>
</html>
<%
Drug.Close()
Set Drug = Nothing
%>
<%
updateddate.Close()
Set updateddate = Nothing
%>
