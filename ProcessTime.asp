<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->
<%

Function CVNULL(Number)
		if number = "0" then
		CVNULL = 1 
		elseif isnull(number) = true then 
		CVNULL = 1 
		else
		CVNULL = number
		end if
End Function

Function CVZero(Number)
		if number = "0" then
		CVZero= 0
		elseif isnull(number) = true then 
		CVZero =  0
		else
		CVZero = number
		end if
End Function

Dim PO
Dim PO_numRows
Real_PO = request.QueryString("RPO")

Set PO = Server.CreateObject("ADODB.Recordset")
PO.ActiveConnection = MM_INVFlood_STRING

PO.Source = "SELECT MS_PO.PO_NO, MS_PO.REAL_PO, MS_PO.PO_DATE , MS_PO.DOC_NO, COMPANY.COMPANY_NAME_PO, MS_PO.TOTAL_ITEM, TOTAL_COST, TblPOStatus.StatusName, BILLIN, BILLout, BILLEND, BDG_TYPE.BDGNAME, TBLBUY.BUYNAME FROM TBLBUY INNER JOIN (TblPOStatus INNER JOIN (BDG_TYPE INNER JOIN (TBLED_NED INNER JOIN (COMPANY INNER JOIN MS_PO ON COMPANY.COMPANY_CODE = MS_PO.VENDOR_CODE) ON TBLED_NED.EDCODE = MS_PO.ED_NED) ON BDG_TYPE.BDGCODE = MS_PO.BUDGET_TYPE) ON TblPOStatus.StatusCode = MS_PO.STATUS) ON TBLBUY.BUYCODE = MS_PO.BUY_METHOD WHERE REAL_PO='" & real_po & "'"

PO.CursorType = 3
PO.CursorLocation = 3
PO.LockType = 1
PO.Open()

PO_numRows = 0
%>
<%
Dim POC
Dim POC_numRows

Set POC = Server.CreateObject("ADODB.Recordset")
POC.ActiveConnection = MM_INVFlood_STRING

POC.Source = "SELECT MS_PO_C.WORKING_CODE, INV_MD.DRUG_NAME, REMAIN, USED_RATE, QTY_ORDER/PACK_RATIO1 AS QTY_O, PACK_RATIO1, MS_PO_C.PO_UNIT, BUY_UNIT_COST, BUY_VALUE, MS_PO_C.QTY_FREE, PACK_RATIO2, MS_PO_C.PO_C_NOTE, MS_PO_C.PO_NO FROM INV_MD INNER JOIN MS_PO_C ON INV_MD.WORKING_CODE = MS_PO_C.WORKING_CODE WHERE  MS_PO_C.PO_NO='" & PO("PO_NO") & "'"

POC.CursorType = 3
POC.CursorLocation = 3
POC.LockType = 1
POC.Open()

POC_numRows = 0

Dim IVOC
Dim IVOC_numRows

Set IVOC = Server.CreateObject("ADODB.Recordset")
IVOC.ActiveConnection = MM_INVFlood_STRING

IVOC.Source = "SELECT MS_IVO.RECEIVE_NO, MS_IVO.INVOICE_NO, MS_IVO.INVOICE_DATE, MS_IVO.DATE_RECEIVE, MS_IVO_C.MANUFAC_CODE, BUY_UNIT_COST, [QTY_ORDER]/[PACK_RATIO1] AS QTY_R, PACK_RATIO1, MS_IVO_C.EXPIRED_DATE1, MS_IVO_C.LOCATION1, QTY_FREE, PACK_RATIO2, MS_IVO_C.EXPIRED_DATE2, MS_IVO_C.LOCATION2, MS_IVO_C.WORKING_CODE, MS_IVO.PO_NO, MS_IVO_C.LOTNO FROM MS_IVO_C INNER JOIN MS_IVO ON MS_IVO_C.RECEIVE_NO = MS_IVO.RECEIVE_NO WHERE MS_IVO.PO_NO='" & PO("REAL_PO") & "'"  

IVOC.CursorType = 3
IVOC.CursorLocation = 3
IVOC.LockType = 1
IVOC.Open()

IVOC_numRows = 0

%>

<%
Dim Repeat1__numRows
Dim Repeat1__index

Repeat1__numRows = -1
Repeat1__index = 0
PO_numRows = PO_numRows + Repeat1__numRows
%>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<title>ระบบรายงานปริมาณยาและเวชภัณฑ์คงคลัง</title>
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

<div align="right">


<br>
<br>
</div>
<table width="90%" border="0" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="12%"><div align="left">เลขที่ใบสั่งซื้อ</div></td>
    <td width="13%"><%=(PO.Fields.Item("REAL_PO").Value)%></td>
    <td width="11%">เลขที่เอกสาร</td>
    <td width="17%"><%=(PO.Fields.Item("DOC_NO").Value)%></td>
    <td width="17%">บริษัท</td>
    <td width="30%"><%=(PO.Fields.Item("COMPANY_NAME_PO").Value)%></td>
  </tr>
  <tr>
    <td><div align="left">วันที่ออกใบสั่งซื้อ</div></td>
    <td><%=(PO.Fields.Item("PO_DATE").Value)%></td>
    <td>ประเภทงบ</td>
    <td><%=(PO.Fields.Item("BDGNAME").Value)%></td>
    <td>วิธีจัดซื้อ</td>
    <td><%=(PO.Fields.Item("BUYNAME").Value)%></td>
  </tr>
  <tr>
    <td>วันที่ส่งใบสั่งซื้อ</td>
    <td>&nbsp;</td>
    <td>จำนวนรายการ</td>
    <td><%=(PO.Fields.Item("TOTAL_ITEM").Value)%></td>
    <td>มูลค่ารวม</td>
    <td><%=formatnumber((PO.Fields.Item("TOTAL_COST").Value),2)%></td>
  </tr>
  <tr>
    <td>วันที่รับของ</td>
    <td><%=(PO.Fields.Item("BILLIN").Value)%></td>
    <td>วันที่ส่งตั้งเบิก</td>
    <td><%=(PO.Fields.Item("BILLOUT").Value)%></td>
    <td>วันที่แปลงหนี้ 100% </td>
    <td><%=(PO.Fields.Item("BILLEND").Value)%></td>
  </tr>
  <tr>
    <td>&nbsp;</td>
    <td>&nbsp;</td>
    <td>&nbsp;</td>
    <td>&nbsp;</td>
    <td>สถานะใบสั่งซื้อ</td>
    <td><%=(PO.Fields.Item("StatusName").Value)%></td>
  </tr>
</table>
<div align="center">
  <p>&nbsp;</p>
  <p>รายละเอียดการสั่งซื้อ</p>
</div>
<table width="95%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="3%"><div align="center">No. </div></td>
	<td width="9%"><div align="center">รหัสยา</div></td>
	<td width="25%"><div align="center">ชื่อยา </div></td>
    <td width="16%"><div align="center">จำนวนสั่ง</div></td>
    <td width="7%"><div align="center">ราคาต่อหน่วย</div></td>
    <td width="9%"><div align="center">ราคารวม</div></td>
    <td width="11%"><div align="center">
      <p>จำนวนแถม</p>
    </div></td>
  </tr>
  <%  runno = 1
While ((Repeat1__numRows <> 0) AND (NOT POC.EOF)) 
%>
<tr> 


    <td><div align="center">
        <%response.write(runno)%>
    .  </div></td>
	<td><div align="center"><%=(POC.Fields.Item("WORKING_CODE").Value)%></div></td>
      <td><div align="center">
        <div align="left"><%=(POC.Fields.Item("DRUG_NAME").Value)%></div>
      </div></td>
    <td ><div align="center"><%=formatnumber(POC.Fields.Item("QTY_O").Value,0)%> x  <%=formatnumber(POC.Fields.Item("PACK_RATIO1").Value,0)%>   <%=(POC.Fields.Item("PO_UNIT").Value)%></div></td>
    <td ><div align="center"><%=formatnumber(POC.Fields.Item("BUY_UNIT_COST").Value,2)%></div></td>
	    <td><div align="center"><%=formatnumber(POC.Fields.Item("BUY_VALUE").Value,2)%></div></td>
	    <td><div align="center"><%=formatnumber(CDbl(CVZero(POC.Fields.Item("QTY_FREE").Value))/CDbl(CVNULL(POC.Fields.Item("PACK_RATIO2").Value)),0)%></div></td>
  </tr>
	    <%
  Repeat1__index=Repeat1__index+1
at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1
  POC.MoveNext()
Wend
%>
</table>

<p align="center">รายละเอียดการรับของ</p>
<table width="95%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="2%"><div align="center">No. </div></td>
    <td width="11%"><div align="center">เลขที่รับ</div></td>
    <td width="9%"><div align="center">วันที่รับ</div></td>
    <td width="12%"><div align="center">เลขที่ใบส่งของ</div></td>
    <td width="10%"><div align="center">วันที่ในใบส่งของ</div></td>
    <td width="11%"><div align="center">รหัสยา</div></td>
    <td width="9%"><div align="center">จำนวนรับ</div></td>
    <td width="9%"><div align="center">ราคาทุน</div></td>
    <td width="9%"><div align="center">เลขที่ผลิต</div></td>
    <td width="9%"><div align="center">วันหมดอายุ</div></td>
    <td width="9%"><div align="center">จำนวนแถม</div></td>
  </tr>
  <%  runno = 1
While ((Repeat1__numRows <> 0) AND (NOT IVOC.EOF)) 
%>
  <tr>
    <td><div align="center">
      <%response.write(runno)%>
      . </div></td>
    <td><div align="center"><%=(IVOC.Fields.Item("RECEIVE_NO").Value)%></div></td>
    <td><div align="center"><%=(IVOC.Fields.Item("DATE_RECEIVE").Value)%></div></td>
    <td><div align="center">
      <div align="center"><%=(IVOC.Fields.Item("INVOICE_NO").Value)%></div>
    </div></td>
    <td ><div align="center"><%=(IVOC.Fields.Item("INVOICE_DATE").Value)%></div></td>
    <td ><div align="center"><%=(IVOC.Fields.Item("WORKING_CODE").Value)%></div></td>
    <td><div align="center"><%=FORMATNUMBER(IVOC.Fields.Item("QTY_R").Value,0)%></div></td>
    <td><div align="center"><%=FORMATNUMBER(IVOC.Fields.Item("BUY_UNIT_COST").Value,2)%>/<%=FORMATNUMBER(IVOC.Fields.Item("PACK_RATIO1").Value,0)%></div></td>
    <td><div align="center"><%=(IVOC.Fields.Item("LOTNO").Value)%></div></td>
    <td><div align="center"><%=(IVOC.Fields.Item("EXPIRED_DATE1").Value)%></div></td>
    <td><div align="center"><%=formatnumber(CDbl(CVZero(IVOC.Fields.Item("QTY_FREE").Value))/CDbl(CVNULL(IVOC.Fields.Item("PACK_RATIO2").Value)),0)%></div></td>
  </tr>
  <tr>
    <%
  Repeat1__index=Repeat1__index+1
at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1
  IVOC.MoveNext()
Wend
%>
  </tr>
</table>
<p>&nbsp;</p>
</body>
</html>
<%
PO.Close()
Set PO = Nothing
%>
<%
POC.Close()
Set POC = Nothing

IVOC.Close()
Set IVOC = Nothing
%>
