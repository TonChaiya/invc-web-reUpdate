<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Dim Drug
Dim Drug_numRows

'YYY = request.QueryString("YYY")
YYY = "65"
POStatus = request.QueryString("POstatus")

Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.ActiveConnection = MM_INVFlood_STRING

Drug.Source = "SELECT MS_PO.PO_NO, MS_PO.REAL_PO, MS_PO.PO_DATE , MS_PO.DOC_NO, COMPANY.COMPANY_NAME_PO, MS_PO.TOTAL_ITEM, TOTAL_COST, TblPOStatus.StatusName, BILLIN, BILLout, BILLEND, BILLOUTACC, BILLOUTFIN, BDG_TYPE.BDGNAME, TBLBUY.BUYNAME FROM TBLBUY INNER JOIN (TblPOStatus INNER JOIN (BDG_TYPE INNER JOIN (TBLED_NED INNER JOIN (COMPANY INNER JOIN MS_PO ON COMPANY.COMPANY_CODE = MS_PO.VENDOR_CODE) ON TBLED_NED.EDCODE = MS_PO.ED_NED) ON BDG_TYPE.BDGCODE = MS_PO.BUDGET_TYPE) ON TblPOStatus.StatusCode = MS_PO.STATUS) ON TBLBUY.BUYCODE = MS_PO.BUY_METHOD WHERE STATUS not in ('0','C') and Left(MS_PO.PO_NO,2)='" & YYY & "' ORDER BY MS_PO.REAL_PO;"

Drug.CursorType = 3
Drug.CursorLocation = 3
Drug.LockType = 1
Drug.Open()


%>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<title>ระบบค้างจ่ายเวชภัณฑ์</title>
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
<div class="container" align="center">
		<div class="row">

</div>

<table width="98%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <th width="8%" bgcolor="#FFFF66"><div align="center">รหัสใบสั่งซื้อ</div></th>
    <th width="8%" bgcolor="#FFFF66"><div align="center">วันที่ออกใบสั่งซื้อ</div></th>
    <th width="7%" bgcolor="#FFFF66"><div align="center">มูลค่า</div></th>
    <th width="29%" bgcolor="#FFFF66"><div align="center">บริษัท</div></th>
    <th width="6%" bgcolor="#FFFF66"><div align="center">มูลค่าที่ตรวจรับแล้ว</div></th>
    <th width="9%" bgcolor="#FFFF66"><div align="center">วันที่ส่งตั้งเบิก</div></th>
    <th width="7%" bgcolor="#FFFF66"><div align="center">เลขที่ขออนุมัติ</div></th>
    <th width="7%" bgcolor="#FFFF66"><div align="center">วันที่ตัดจ่าย</div></th>
    <th width="9%" bgcolor="#FFFF66"><div align="center">วันที่ปิดบัญชี</div></th>
  </tr>
  <%  runno = 1
While NOT Drug.EOF
%>

    <tr> 

		<td><div align="center"><a href="PODetail.asp?RPO=<%=(Drug("REAL_PO"))%>"><%=(Drug("REAL_PO"))%></a></div></td>
		<td><div align="center"><%=Drug("PO_DATE")%></div></td>
		<td><div align="center"><%=formatnumber(Drug("TOTAL_COST"))%></div></td>
		<td><div align="left"><%=Drug("COMPANY_NAME_PO")%></div></td>
		<td><div align="center"></div></td>
	    <td><div align="center"><%=Drug("BILLOUTACC")%></div></td>
	    <td><div align="center"></div></td>
	    <td><div align="center"></div></td>
	    <td><div align="center"></div></td>
	      
<tr>
   
<%
  
  runno = runno +1
  Drug.MoveNext()
Wend
%>

</table>
<p>&nbsp;</p>

</div>
</div>

</body>
</html>
<%
Drug.Close()
Set Drug = Nothing
%>

