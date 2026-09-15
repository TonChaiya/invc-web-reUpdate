<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Dim INVMD
Dim INVMD_numRows

Set INVMD = Server.CreateObject("ADODB.Recordset")
INVMD.ActiveConnection = MM_INVFlood_STRING


	code = request.QueryString("code")
	DEPT = request.QueryString("DEPT_ID")

INVMD.Source = "SELECT *  FROM INV_MD WHERE WORKING_CODE='" & code & "'"

INVMD.CursorType = 3
INVMD.CursorLocation = 3
INVMD.LockType = 1
INVMD.Open()

INVMD_numRows = 0


Dim CARDSUBS
Dim CARDSUBS_numRows

Set CARDSUBS= Server.CreateObject("ADODB.Recordset")
CARDSUBS.ActiveConnection = MM_INVFlood_STRING

CARDSUBS.Source = "SELECT *  FROM CARD_SUBS WHERE WORKING_CODE='" & code & "' AND DEPT_ID='" & DEPT & "' ORDER BY RECORD_NUMBER DESC"

CARDSUBS.CursorType = 3
CARDSUBS.CursorLocation = 3
CARDSUBS.LockType = 1
CARDSUBS.Open()

CARDSUBS_numRows = 0

Dim SUBDPT
Dim SUBDPT_numRows

Set SUBDPT = Server.CreateObject("ADODB.Recordset")
SUBDPT.ActiveConnection = MM_INVFlood_STRING

SUBDPT.Source = "SELECT *  FROM DEPT_ID WHERE DEPT_ID='" & DEPT & "'"

SUBDPT.CursorType = 3
SUBDPT.CursorLocation = 3
SUBDPT.LockType = 1
SUBDPT.Open()

SUBDPT_numRows = 0


Dim Repeat2__numRows
Dim Repeat2__index

Repeat2__numRows = -1
Repeat2__index = 0



Dim SUBSTOCKC
Dim SUBSTOCKC_numRows

Set SUBSTOCKC = Server.CreateObject("ADODB.Recordset")
SUBSTOCKC.ActiveConnection = MM_INVFlood_STRING

SUBSTOCKC.Source = "SELECT *  FROM SUBSTOCK_C WHERE WORKING_CODE='"  & code & "' AND DEPT_ID='" & DEPT & "' order by  RECORD_NUMBER DESC"

SUBSTOCKC.CursorType = 3
SUBSTOCKC.CursorLocation = 3
SUBSTOCKC.LockType = 1
SUBSTOCKC.Open()

SUBSTOCKC_numRows = 0

Dim Repeat3__numRows
Dim Repeat3__index

Repeat3__numRows = -1
Repeat3__index = 0
SUBSTOCKC_numRows = SUBSTOCKC_numRows + Repeat3__numRows

Dim Repeat4__numRows
Dim Repeat4__index

Repeat4__numRows = -1
Repeat4__index = 0
CARDSUBS_numRows = CARDSUBS_numRows + Repeat4__numRows
%>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<title>ปริมาณยาและเวชภัณฑ์คงคลัง</title>
    <!-- Bootstrap -->
    
    <link rel="stylesheet" href="css/bootstrap.min.css"> 
	<script src="js/jquery-3.2.1.min.js"></script>
	<script src="js/bootstrap.min.js"></script>
    <!-- HTML5 shim and Respond.js for IE8 support of HTML5 elements and media queries -->
    <!-- WARNING: Respond.js doesn't work if you view the page via file:// -->
    <!--[if lt IE 9]>
      <script src="https://oss.maxcdn.com/html5shiv/3.7.3/html5shiv.min.js"></script>
      <script src="https://oss.maxcdn.com/respond/1.4.2/respond.min.js"></script>
    <![endif]-->
<!--#include file="menu.asp" -->
</head>

<body>

<br>
<div class="container" align="center">
		<div class="row">

<p align="center">รายละเอียดยาที่อยู่ในคลังย่อย <%=(SUBDPT.Fields.Item("DEPT_NAME").Value)%></p> 
<table width="95%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="10%" bgcolor="#FFFF66"><div align="center">รหัสยา</div></td>
    <td width="13%" bgcolor="#FFFFCC"><div align="center"><span class="style2"><strong><%=(INVMD.Fields.Item("WORKING_CODE").Value)%></strong></span></div></td>
    <td width="10%" bgcolor="#FFFF66"><div align="center">&#3594;&#3639;&#3656;&#3629;&#3618;&#3634;<br />
    </div></td>
    <td width="25%" bgcolor="#FFFFCC"><div align="center"><strong><%=(INVMD.Fields.Item("drug_name").Value)%></strong></div></td>
  </tr>
  <%  runno = 1
'While ((Repeat1__numRows <> 0) AND (NOT INVMD.EOF)) 
%>
   
<%
 ' Repeat1__index=Repeat1__index+1
  'at1__index=Repeat1__index+1
  'Repeat1__numRows=Repeat1__numRows-1
  'runno = runno +1
  'INVMD.MoveNext()
'Wend
%>
</table>

<p align="center">&nbsp;</p>

<table width="95%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="3%" bgcolor="#FFFF66"><div align="center">ลำดับ</div></td>
    <td width="25%" bgcolor="#FFFF66"><div align="center">ชื่อการค้า</div></td>
    <td width="6%" bgcolor="#FFFF66"><div align="center">คงเหลือ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">ขนาดบรรจุ</div></td>
    <td width="9%" bgcolor="#FFFF66"><div align="center">วันหมดอายุ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">เลขที่ผลิต</div></td>
    <td width="5%" bgcolor="#FFFF66"><div align="center">ที่เก็บ</div></td>
    <td width="15%" bgcolor="#FFFF66"><div align="center">ผู้จำหน่าย</div></td>
        <td width="15%" bgcolor="#FFFF66"><div align="center">ผู้ผลิต</div></td>
  </tr>
 
<%  runno = 1
While ((Repeat3__numRows <> 0) AND (NOT SUBSTOCKC.EOF)) 
%>
<%

Dim DRUGVN
Dim DRUGVN_numRows

Set DRUGVN = Server.CreateObject("ADODB.Recordset")
DRUGVN.ActiveConnection = MM_INVFlood_STRING

DRUGVN.Source = "SELECT *  FROM DRUG_VN WHERE WORKING_CODE='" & SUBSTOCKC("WORKING_CODE") & "' AND VENDOR_CODE='" & SUBSTOCKC("VENDOR_CODE") & "' AND MANUFAC_CODE='" & SUBSTOCKC("MANUFAC_CODE") & "' AND PACK_RATIO=" & SUBSTOCKC("PACK_RATIO")

DRUGVN.CursorType = 3
DRUGVN.CursorLocation = 3
DRUGVN.LockType = 1
DRUGVN.Open()

DRUGVN_numRows = 0



Dim VENDORS
Dim VENDORS_numRows

Set VENDORS = Server.CreateObject("ADODB.Recordset")
VENDORS.ActiveConnection = MM_INVFlood_STRING

VENDORS.Source = "SELECT * FROM COMPANY WHERE COMPANY_CODE='" & SUBSTOCKC("VENDOR_CODE")  & "'"

VENDORS.CursorType = 3
VENDORS.CursorLocation = 3
VENDORS.LockType = 1
VENDORS.Open()

VENDORS_numRows = 0

Dim MANUFACS
Dim MANUFACS_numRows

Set MANUFACS = Server.CreateObject("ADODB.Recordset")
MANUFACS.ActiveConnection = MM_INVFlood_STRING

MANUFACS.Source = "SELECT * FROM COMPANY WHERE COMPANY_CODE='" & SUBSTOCKC("MANUFAC_CODE") & "'"

MANUFACS.CursorType = 3
MANUFACS.CursorLocation = 3
MANUFACS.LockType = 1
MANUFACS.Open()

MANUFACS_numRows = 0
%>
	<tr> 
		 <td valign="middle" bgcolor="#FFFFCC">  <div align="center">
        <%response.write(runno)%> </div></td>
		<td bgcolor="#FFFFCC"><div align="center">  <% if not drugvn.eof then response.write (DRUGVN.Fields.Item("TRADE_NAME").Value) %> </div></td>
    	<% dim D, E, F
		D = CLng(SUBSTOCKC.Fields.Item("QTY_ON_HAND").Value)
		E = CLng(SUBSTOCKC.Fields.Item("PACK_RATIO").Value) 
		F = D/E
		%>  
      <td bgcolor="#FFFFCC"><div align="center"><%=F%></div></td>
	  <td bgcolor="#FFFFCC"><div align="center"><%=(SUBSTOCKC.Fields.Item("PACK_RATIO").Value)%></div></td>
	  <td bgcolor="#FFFFCC"><div align="center"><%=(SUBSTOCKC.Fields.Item("EXPIRED_DATE").Value)%></div></td>
      <td bgcolor="#FFFFCC"><div align="center"><%=(SUBSTOCKC.Fields.Item("LOTNO").Value)%></div></td>
	  <td bgcolor="#FFFFCC"><div align="center"><%=(SUBSTOCKC.Fields.Item("LOCATION").Value)%></div></td>
	  <td bgcolor="#FFFFCC"><div align="center"><%=(VENDORS.Fields.Item("COMPANY_NAME").Value)%></div></td>
      <td bgcolor="#FFFFCC"><div align="center"><%=(MANUFACS.Fields.Item("COMPANY_NAME").Value)%></div></td>
	</tr>
<%
  Repeat3__index=Repeat3__index+1
  at3__index=Repeat3__index+1
  Repeat3__numRows=Repeat3__numRows-1
  runno = runno +1
  SUBSTOCKC.MoveNext()
Wend
%>
</table>
</br>

<p align="center">ข้อมูลการเบิกจ่ายของคลังย่อย <%=(SUBDPT.Fields.Item("DEPT_NAME").Value)%> (แสดง 20 รายการล่าสุด)</p>
<table width="95%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="3%" bgcolor="#FFFF66"><div align="center">ลำดับ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">วันที่ทำรายการ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">เบิก/จ่าย</div></td>
    <td width="18%" bgcolor="#FFFF66"><div align="center">หน่วยเบิก</div></td>
    <td width="7%" bgcolor="#FFFF66"><div align="center">จำนวนเบิก/จ่าย</div></td>
        <td width="7%" bgcolor="#FFFF66"><div align="center">ขนาดบรรจุ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">จำนวนคงเหลือ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">เลขที่เอกสาร</div></td>


  </tr>
  <%  runno = 1
Do While ((Repeat4__numRows <> 0) AND (NOT CARDSUBS.EOF)) 
%>

  <tr>
    <td valign="middle" bgcolor="#FFFFCC"><div align="center">
      <%response.write(runno)%>
    </div></td>
    <td bgcolor="#FFFFCC"><div align="left"><%=(CARDSUBS.Fields.Item("OPERATE_DATE").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=(CARDSUBS.Fields.Item("R_S_STATUS").Value)%></div></td>
	<td bgcolor="#FFFFCC"><div align="center"><%=(SUBDPT.Fields.Item("DEPT_NAME").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=CLNG(CARDSUBS.Fields.Item("R_S_QTY").Value)/CLNG(INVMD.Fields.Item("STD_RATIO3").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=CLNG(INVMD.Fields.Item("STD_RATIO3").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=CLNG(CARDSUBS.Fields.Item("REMAIN_QTY").Value)/CLNG(INVMD.Fields.Item("STD_RATIO3").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=(CARDSUBS.Fields.Item("R_S_NUMBER").Value)%></div></td>
  </tr>
  <%
  Repeat4__index=Repeat4__index+1
  at4__index=Repeat4__index+1
  Repeat4__numRows=Repeat4__numRows-1
  runno = runno +1
  if runno>20 then
  exit do
  end if 
  CARDSUBS.MoveNext()
Loop
%>
</table>

	  </div>
  </div>
</body>

<%
INVMD.Close()
Set INVMD = Nothing

SUBSTOCKC.Close()
Set SUBSTOCKC = Nothing

CARDSUBS.Close()
Set CARDSUBS = Nothing

%>