<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Dim Drug
Dim Drug_numRows

YYY = request.QueryString("YYY")
POStatus = request.QueryString("POstatus")

Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.ActiveConnection = MM_INVFlood_STRING

Drug.Source = "SELECT MS_PO.PO_NO, MS_PO.REAL_PO, MS_PO.PO_DATE , MS_PO.DOC_NO, COMPANY.COMPANY_NAME_PO, MS_PO.TOTAL_ITEM, TOTAL_COST, TblPOStatus.StatusName, BILLIN, BILLout, BILLEND, BDG_TYPE.BDGNAME, TBLBUY.BUYNAME FROM TBLBUY INNER JOIN (TblPOStatus INNER JOIN (BDG_TYPE INNER JOIN (TBLED_NED INNER JOIN (COMPANY INNER JOIN MS_PO ON COMPANY.COMPANY_CODE = MS_PO.VENDOR_CODE) ON TBLED_NED.EDCODE = MS_PO.ED_NED) ON BDG_TYPE.BDGCODE = MS_PO.BUDGET_TYPE) ON TblPOStatus.StatusCode = MS_PO.STATUS) ON TBLBUY.BUYCODE = MS_PO.BUY_METHOD WHERE STATUS='" & POstatus & "' and Left(MS_PO.PO_NO,2)='" & YYY & "' ORDER BY MS_PO.REAL_PO;"

Drug.CursorType = 3
Drug.CursorLocation = 3
Drug.LockType = 1
Drug.Open()

Drug_numRows = 0

Dim Repeat1__numRows
Dim Repeat1__index

Repeat1__numRows = -1
Repeat1__index = 0
Drug_numRows = Drug_numRows + Repeat1__numRows
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
<p><div align="center">ข้อมูลใบสั่งซื้อ : <%=(Drug.Fields.Item("STATUSNAME").Value)%></div> </p>
<table width="98%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="8%" bgcolor="#FFFF66"><div align="center">รหัสใบสั่งซื้อ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">วันที่ซื้อ</div></td>
    <td width="10%" bgcolor="#FFFF66"><div align="center">เลขที่เอกสาร</div></td>
    <td width="29%" bgcolor="#FFFF66"><div align="center">บริษัท</div></td>
    <td width="6%" bgcolor="#FFFF66"><div align="center">จำนวนรายการ</div></td>
     <td width="7%" bgcolor="#FFFF66"><div align="center">มูลค่า</div></td>
	 
    <td width="9%" bgcolor="#FFFF66"><div align="center">สถานะ</div></td>
    <td width="7%" bgcolor="#FFFF66"><div align="center">วันที่รับของ</div></td>
   
    <td width="7%" bgcolor="#FFFF66"><div align="center">
      <p>วันที่ส่งตั้งเบิก</p>
      </div></td>
    <td width="9%" bgcolor="#FFFF66"><div align="center">
      <p>วันที่โอนหนี้ 100%</p>
      </div></td>
  </tr>
  <%  runno = 1
While ((Repeat1__numRows <> 0) AND (NOT Drug.EOF)) 
%>

    <tr> 

    <td>  <div align="center"><a href="PODetail.asp?RPO=<%=(Drug.Fields.Item("REAL_PO").Value)%>"><%=(Drug.Fields.Item("REAL_PO").Value)%></a></div></td>
      <td><div align="center"><%=(Drug.Fields.Item("PO_DATE").Value)%></div></td>
      <td><div align="center"><%=(Drug.Fields.Item("DOC_NO").Value)%></div></td>
      
      <td><div align="left"><%=(Drug.Fields.Item("COMPANY_NAME_PO").Value)%></div></td>
    <td><div align="center"><span class="style2">
	      </span>
	        <div align="left"><span class="style2">
	          <div align="center"><%=(Drug.Fields.Item("TOTAL_ITEM").Value)%></div>
            </span></div>
	        <span class="style2"></span></div></td>

	    <td><div align="center"><%=formatnumber((Drug.Fields.Item("TOTAL_COST").Value),2)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("STATUSNAME").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("BILLIN").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("BILLOUT").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("BILLEND").Value)%></div></td>
	      <%'response.write(B)%>
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
<p>&nbsp;</p>

</div>
</div>

</body>
</html>
<%
Drug.Close()
Set Drug = Nothing
%>

