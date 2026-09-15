<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->
<!--#include file="include/inc_functions.asp" -->
<%
Dim Drug
Dim Drug_numRows

Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.ActiveConnection = MM_INVFlood_STRING
Drug.Source = "SELECT TBLED_NED.EDNAME, Sum(INV_MD.TOTAL_VALUE) AS SumOfTOTAL_VALUE, TBLED_NED.EDCODE FROM INV_MD INNER JOIN TBLED_NED ON INV_MD.ED_NED = TBLED_NED.EDCODE GROUP BY TBLED_NED.EDNAME, TBLED_NED.EDCODE ORDER BY TBLED_NED.EDCODE;"
Drug.CursorType = 3
Drug.CursorLocation = 3
Drug.LockType = 1
Drug.Open()

Drug_numRows = 0


Dim Budget
Dim Budget_numRows

Set Budget = Server.CreateObject("ADODB.Recordset")
Budget.ActiveConnection = MM_INVFlood_STRING
Budget.Source = "SELECT BDG_TYPE.BDGNAME, BUDGET.money, BUDGET.deb1, BUDGET.deb2, BUDGET.year, BUDGET.BudgetOpen FROM BDG_TYPE INNER JOIN BUDGET ON BDG_TYPE.BDGCODE = BUDGET.type WHERE (((BUDGET.BudgetOpen)='O'));"
Budget.CursorType = 3
Budget.CursorLocation = 3
Budget.LockType = 1
Budget.Open()

Budget_numRows = 0

Dim M_STOCK
Dim M_STOCK_numRows

Set M_STOCK = Server.CreateObject("ADODB.Recordset")
M_STOCK.ActiveConnection = MM_INVFlood_STRING
M_STOCK.Source = "SELECT TOP 1 MBS_RE_M.YEAR, MBS_RE_M.MONTH, Sum(MBS_RE_M.SALE_VALUE) AS SumOfSALE_VALUE, Sum(MBS_RE_M.REMAIN_VALUE) AS SumOfREMAIN_VALUE FROM MBS_RE_M GROUP BY MBS_RE_M.YEAR, MBS_RE_M.MONTH ORDER BY MBS_RE_M.YEAR DESC , MBS_RE_M.MONTH DESC;"
M_STOCK.CursorType = 3
M_STOCK.CursorLocation = 3
M_STOCK.LockType = 1
M_STOCK.Open()

M_STOCK_numRows = 0


Dim ITEM_VALUE1
Dim ITEM_VALUE1_numRows

Set ITEM_VALUE1 = Server.CreateObject("ADODB.Recordset")
ITEM_VALUE1.ActiveConnection = MM_INVFlood_STRING
ITEM_VALUE1.Source = "SELECT Count(INV_MD.WORKING_CODE) AS ITEM, INV_MD.ED_NED, Sum(INV_MD.TOTAL_VALUE) AS VALUE FROM INV_MD GROUP BY INV_MD.ED_NED, INV_MD.NOUSE, INV_MD.OUT_OF_LIST, INV_MD.PO_INDIVIDUAL HAVING (((INV_MD.ED_NED)='1') AND ((INV_MD.NOUSE) Is Null) AND ((INV_MD.OUT_OF_LIST) Is Null) AND ((INV_MD.PO_INDIVIDUAL) Is Null));"
ITEM_VALUE1.CursorType = 3
ITEM_VALUE1.CursorLocation = 3
ITEM_VALUE1.LockType = 1
ITEM_VALUE1.Open()

ITEM_VALUE1_numRows = 0

Dim ITEM_VALUE2
Dim ITEM_VALUE2_numRows

Set ITEM_VALUE2 = Server.CreateObject("ADODB.Recordset")
ITEM_VALUE2.ActiveConnection = MM_INVFlood_STRING
ITEM_VALUE2.Source = "SELECT Count(INV_MD.WORKING_CODE) AS ITEM, INV_MD.ED_NED, Sum(INV_MD.TOTAL_VALUE) AS VALUE FROM INV_MD GROUP BY INV_MD.ED_NED, INV_MD.NOUSE, INV_MD.OUT_OF_LIST, INV_MD.PO_INDIVIDUAL HAVING (((INV_MD.ED_NED)='2') AND ((INV_MD.NOUSE) Is Null) AND ((INV_MD.OUT_OF_LIST) Is Null) AND ((INV_MD.PO_INDIVIDUAL) Is Null));"
ITEM_VALUE2.CursorType = 3
ITEM_VALUE2.CursorLocation = 3
ITEM_VALUE2.LockType = 1
ITEM_VALUE2.Open()

ITEM_VALUE2_numRows = 0

Dim POSTATUS
Dim POSTATUS_numRows

Set POSTATUS = Server.CreateObject("ADODB.Recordset")
POSTATUS.ActiveConnection = MM_INVFlood_STRING
POSTATUS.Source = "SELECT  TblPOStatus.StatusCode, TblPOStatus.StatusName, Count(MS_PO.PO_NO) AS ITEM, Left([PO_NO],2) AS Expr1, sum([TOTAL_COST]) as Total FROM TblPOStatus INNER JOIN MS_PO ON TblPOStatus.StatusCode = MS_PO.STATUS GROUP BY  TblPOStatus.StatusCode, TblPOStatus.StatusName, Left([PO_NO],2) HAVING (((Left([PO_NO],2))='" &  right(budget.Fields.Item("year").Value,2) & "')) ORDER BY TblPOStatus.StatusCode;"
POSTATUS.CursorType = 3
POSTATUS.CursorLocation = 3
POSTATUS.LockType = 1
POSTATUS.Open()

POSTATUS_numRows = 0

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
<title>ระบบรายงานมูลค่าคงคลังและบประมาณจัดซื้อ</title>
<style type="text/css">
<!--
.style11 {font-size: 14px}
.style12 {font-weight: bold}
.style15 {font-size: 12px}
-->
</style>
<!--#include file="menu.asp" -->
</head>

<body>

<br>
<br>
</div>
<table width="90%" border="0" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td colspan="2"><div align="center" class="style11"><strong>มูลค่า&#3618;&#3634;&#3649;&#3621;&#3632;&#3648;&#3623;&#3594;&#3616;&#3633;&#3603;&#3601;&#3660;&#3588;&#3591;ค&#3621;&#3633;&#3591; ณ วันที่ <%=date()%> เวลา <%=time()%></strong></div>  </td>
  </tr>


</table>
<table width="90%" border="0" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td><div align="center">

      </div></td>
  </tr>
</table>

<br>

<table width="60%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="52%" bgcolor="#FFFF66"><div align="center">ประเภท</div></td>
    <td width="48%" bgcolor="#FFFF66"><div align="center">
 มูลค่าคงคลัง
    </div></td>
  </tr>
  <%  runno = 1
  		totalvalue = 0
While ((Repeat1__numRows <> 0) AND (NOT Drug.EOF)) 
%>
  <td bgcolor="#FFFFCC"><%response.write(runno)%>
    . <%=(Drug.Fields.Item("EDNAME").Value)%> </td>
      <td bgcolor="#FFFFCC"><div align="center"><%=formatnumber((Drug.Fields.Item("SumOfTOTAL_VALUE").Value),2)%> </div>
          <div align="center"><span class="style2"> </span>
              <div align="left">
                <div align="center"></div>
              </div>
          </div></td>
  <tr>
    <%
	
  Repeat1__index=Repeat1__index+1
  at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1
  totalvalue = totalvalue + CDbl(Drug.Fields.Item("SumOfTOTAL_VALUE").Value)
  Drug.MoveNext()
Wend
%>
  </tr>
  <tr>
    <td width="52%" bgcolor="#FFFF66"><div align="center"><strong>รวม</strong></div></td>
    <td width="48%" bgcolor="#FFFF66"><div align="center" class="style12">
      <%response.write (formatnumber(totalvalue,2))%>
    </div></td>
  </tr>
</table>
<br>
<table width="60%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="52%" bgcolor="#FFFF66"><div align="center">ตัวชี้วัด</div></td>
    <td width="48%" bgcolor="#FFFF66"><div align="center">
 ผลการดำเนินงาน</div></td>
  </tr>

      <td bgcolor="#FFFFCC"><span class="style15">จำนวนเดือนสำรองคลัง (ข้อมูลเดือน <%=M_STOCK.Fields.Item("MONTH").Value%> ปี <%=M_STOCK.Fields.Item("YEAR").Value + 543%>)</span></td>
      <td bgcolor="#FFFFCC"><div align="center" class="style15"><%= formatnumber(CDbl(M_STOCK.Fields.Item("sumofremain_value").Value) / CDbl(M_STOCK.Fields.Item("SumOfSALE_VALUE").Value),2,-1)%> เดือน </div>		</td>
      <tr>
        <td bgcolor="#FFFFCC">สัดส่วนรายการยา (ED:NED)</td>
        <td bgcolor="#FFFFCC"><div align="center"><span class="style15"><%= formatnumber(CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value),0)%> : <%= formatnumber(CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value),0)%> (<%= formatnumber(CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value) / (CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value) + CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value))*100,2)%> : <%= formatnumber(CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value) / (CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value) + CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value))*100,2)%>) รวม  <%= formatnumber(CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value) + CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value),0)%> รายการ </span></div></td>
  <tr>
    <td bgcolor="#FFFFCC">สัดส่วนมูลค่ายา (ED:NED)</td>
        <td bgcolor="#FFFFCC"><div align="center"><span class="style15"><%= formatnumber(CDbl(ITEM_VALUE1.Fields.Item("VALUE").Value) / (CDbl(ITEM_VALUE1.Fields.Item("VALUE").Value) + CDbl(ITEM_VALUE2.Fields.Item("VALUE").Value))*100,2)%> : <%= formatnumber(CDbl(ITEM_VALUE2.Fields.Item("VALUE").Value) / (CDbl(ITEM_VALUE1.Fields.Item("VALUE").Value) + CDbl(ITEM_VALUE2.Fields.Item("VALUE").Value))*100,2)%></span></div></td>
</table>

<p align="center" class="style11"><strong>มูลค่ายาและเวชภัณฑ์คงคลัง ณ คลังยาย่อย</strong></p>
<table width="60%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="52%" bgcolor="#FFFF66"><div align="center">ตัวชี้วัด</div></td>
    <td width="48%" bgcolor="#FFFF66"><div align="center">
 ผลการดำเนินงาน</div></td>
  </tr>

      <td bgcolor="#FFFFCC"><span class="style15">จำนวนเดือนสำรองคลัง (ข้อมูลเดือน <%=M_STOCK.Fields.Item("MONTH").Value%> ปี <%=M_STOCK.Fields.Item("YEAR").Value + 543%>)</span></td>
      <td bgcolor="#FFFFCC"><div align="center" class="style15"><%= formatnumber(CDbl(M_STOCK.Fields.Item("sumofremain_value").Value) / CDbl(M_STOCK.Fields.Item("SumOfSALE_VALUE").Value),2,-1)%> เดือน </div>		</td>
      <tr>
        <td bgcolor="#FFFFCC">สัดส่วนรายการยา (ED:NED)</td>
        <td bgcolor="#FFFFCC"><div align="center"><span class="style15"><%= formatnumber(CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value),0)%> : <%= formatnumber(CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value),0)%> (<%= formatnumber(CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value) / (CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value) + CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value))*100,2)%> : <%= formatnumber(CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value) / (CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value) + CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value))*100,2)%>) รวม  <%= formatnumber(CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value) + CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value),0)%> รายการ </span></div></td>
  <tr>
    <td bgcolor="#FFFFCC">สัดส่วนมูลค่ายา (ED:NED)</td>
        <td bgcolor="#FFFFCC"><div align="center"><span class="style15"><%= formatnumber(CDbl(ITEM_VALUE1.Fields.Item("VALUE").Value) / (CDbl(ITEM_VALUE1.Fields.Item("VALUE").Value) + CDbl(ITEM_VALUE2.Fields.Item("VALUE").Value))*100,2)%> : <%= formatnumber(CDbl(ITEM_VALUE2.Fields.Item("VALUE").Value) / (CDbl(ITEM_VALUE1.Fields.Item("VALUE").Value) + CDbl(ITEM_VALUE2.Fields.Item("VALUE").Value))*100,2)%></span></div></td>
</table>


 <p align="center" class="style11"><strong>รายงานการใช้งบจัดซื้อ ปีงบประมาณ <%=(budget.Fields.Item("year").Value)%></strong></p>
<table width="60%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="20%" bgcolor="#FFFF66"><div align="center">ประเภทงบ</div></td>
    <td width="20%" bgcolor="#FFFF66"><div align="center">วงเงินที่ได้รับจัดสรร</div></td>
    <td width="20%" bgcolor="#FFFF66"><div align="center">หนี้สินผูกพัน</div></td>
    <td width="20%" bgcolor="#FFFF66"><div align="center">หนี้ 100% </div></td>
    <td width="20%" bgcolor="#FFFF66"><div align="center">วงเงินคงเหลือ</div></td>
  </tr>
  <%  runno = 1 
  t1 = 0
  t2 = 0
  t3=0
  t4 = 0
  Y = right(budget.Fields.Item("year").Value,2)
While ((Repeat1__numRows <> 0) AND (NOT Budget.EOF)) 
%>
  <tr>
    <td bgcolor="#FFFFCC"><%response.write(runno)%>
      . <%=Budget.Fields.Item("BDGNAME")%> </td>
    <td bgcolor="#FFFFCC"><div align="center"><%=formatnumber((Budget.Fields.Item("money").Value),2)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=formatnumber((Budget.Fields.Item("deb1").Value),2)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=formatnumber((Budget.Fields.Item("deb2").Value),2)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=formatnumber(CDbl(Budget.Fields.Item("money").Value) - CDbl(Budget.Fields.Item("deb1").Value) - CDbl(Budget.Fields.Item("deb2").Value),2)%><span class="style2"> </span> </div></td>
  </tr>
  <tr>
    <%
	
  Repeat1__index=Repeat1__index+1
  at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1 
  t1 = t1 + CDbl(Budget.Fields.Item("money").Value) 
  t2 = t2 +  CDbl(Budget.Fields.Item("deb1").Value) 
  t3 = t3 +  CDbl(Budget.Fields.Item("deb2").Value)
  t4 = t4 +  (CDbl(Budget.Fields.Item("money").Value) - CDbl(Budget.Fields.Item("deb1").Value) - CDbl(Budget.Fields.Item("deb2").Value))
  Budget.MoveNext()
Wend
%>
  </tr>
  <tr>
    <td width="20%" bgcolor="#FFFF66"><div align="center"><strong>รวม</strong></div></td>
    <td width="20%" bgcolor="#FFFF66"><div align="center"><strong>
      <%response.write (formatnumber(t1,2))%>
    </strong></div></td>
    <td width="20%" bgcolor="#FFFF66"><div align="center"><strong>
      <%response.write (formatnumber(t2,2))%>
    </strong></div></td>
    <td width="20%" bgcolor="#FFFF66"><div align="center"><strong>
      <%response.write (formatnumber(t3,2))%>
    </strong></div></td>
    <td width="20%" bgcolor="#FFFF66"><div align="center"><strong>
        <%response.write (formatnumber(t4,2))%>
    </strong></div></td>
  </tr>
</table>
<p align="center" class="style11"><strong>รายงานสถานะใบสั่งซื้อ</strong></p>
<table width="60%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="35%" bgcolor="#FFFF66"><div align="center">สถานะใบสั่งซื้อ</div></td>
    <td width="32%" bgcolor="#FFFF66"><div align="center">จำนวนใบสั่งซื้อ</div></td>
    <td width="33%" bgcolor="#FFFF66"><div align="center">มูลค่า</div></td>
  </tr>
  <%  runno = 1
  		totalvalue = 0
		totalcost = 0 
		
While ((Repeat1__numRows <> 0) AND (NOT POSTATUS.EOF)) 
%>
  <tr><td bgcolor="#FFFFCC"><%response.write(runno)%>
    . <%=(POSTATUS.Fields.Item("STATUSNAME").Value)%> </td>
      <td bgcolor="#FFFFCC"><div align="center"><a href="PO.asp?POstatus=<%=POSTATUS.Fields.Item("StatusCode").Value%>&YYY=<%=Y%>"><%=FORMATNUMBER(POSTATUS.Fields.Item("ITEM").Value,0)%></a></div></td>
      <td bgcolor="#FFFFCC"><div align="center"><a href="PO.asp?POstatus=<%=POSTATUS.Fields.Item("StatusCode").Value%>&amp;YYY=<%=Y%>"><%=FORMATNUMBER(POSTATUS.Fields.Item("TOTAL").Value,2)%></a></div>
          <div align="center"><span class="style2"> </span>
              <div align="left">
                <div align="center"></div>
              </div>
          </div></td>
  <tr>
    <%
	
  Repeat1__index=Repeat1__index+1
  at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1
  totalvalue = totalvalue + CDbl(POSTATUS.Fields.Item("ITEM").Value)
  totalcost = totalcost + CDbl(POSTATUS.Fields.Item("TOTAL").Value)
  POSTATUS.MoveNext()
Wend
%>
  </tr>
  <tr>
    <td width="35%" bgcolor="#FFFF66"><div align="center"><strong>รวม</strong></div></td>
    <td width="32%" bgcolor="#FFFF66"><div align="center"><span class="style12">
      <%response.write (formatnumber(totalvalue,0))%>
    </span></div></td>
    <td width="33%" bgcolor="#FFFF66"><div align="center" class="style12">
      <%response.write (formatnumber(totalcost,2))%>
    </div></td>
  </tr>
</table>
</body>
</html>
<%
Drug.Close()
Set Drug = Nothing

Budget.Close()
Set Budget = Nothing

M_STOCK.Close()
set M_STOCK = Nothing
 
 ITEM_VALUE1.close()
 set ITEM_VALUE1 = Nothing

 ITEM_VALUE2.close()
 set ITEM_VALUE2 = Nothing
 
 POSTATUS.Close()
 set POSTATUS = Nothing
 
%>

