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


Dim Countdrug
Dim Countdrug_numRows

Set Countdrug = Server.CreateObject("ADODB.Recordset")
Countdrug.ActiveConnection = MM_INVFlood_STRING
Countdrug.Source = "SELECT Count(INV_MD.WORKING_CODE) AS ITEM, INV_MD.ED_NED, Sum(INV_MD.TOTAL_VALUE) AS VALUE FROM INV_MD WHERE INV_MD.NOUSE Is Null AND INV_MD.OUT_OF_LIST Is Null AND INV_MD.PO_INDIVIDUAL Is Null GROUP BY INV_MD.ED_NED, INV_MD.NOUSE, INV_MD.OUT_OF_LIST, INV_MD.PO_INDIVIDUAL order by INV_MD.ED_NED;"
Countdrug.CursorType = 3
Countdrug.CursorLocation = 3
Countdrug.LockType = 1
Countdrug.Open()

Countdrug_numRows = 0


Dim SUBDrug
Dim SUBDrug_numRows

Set SUBDrug = Server.CreateObject("ADODB.Recordset")
SUBDrug.ActiveConnection = MM_INVFlood_STRING
SUBDrug.Source = "SELECT DEPT_ID.DEPT_NAME, Sum(TOTAL_VALUE) AS Sumtotal FROM SUBSTOCK,DEPT_ID WHERE DEPT_ID.DEPT_ID = SUBSTOCK.DEPT_ID AND TOTAL_VALUE is not null GROUP BY DEPT_NAME ;"
SUBDrug.CursorType = 3
SUBDrug.CursorLocation = 3
SUBDrug.LockType = 1
SUBDrug.Open()

SUBDrug_numRows = 0


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


Dim REMAIN_STOCK
Dim REMAIN_STOCK_numRows

Set REMAIN_STOCK = Server.CreateObject("ADODB.Recordset")
REMAIN_STOCK.ActiveConnection = MM_INVFlood_STRING
REMAIN_STOCK.Source = "SELECT SUM(MNTH_SUM.TOTAL_VALUE) AS M_REMAIN FROM MNTH_SUM WHERE MNTH_SUM.YEAR='" & M_STOCK("YEAR") & "' AND MNTH_SUM.MONTH ='" & M_STOCK("MONTH") & "'"
REMAIN_STOCK.CursorType = 3
REMAIN_STOCK.CursorLocation = 3
REMAIN_STOCK.LockType = 1
REMAIN_STOCK.Open()

REMAIN_STOCK_numRows = 0



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

Dim POProcess
Dim POProcess_numRows

Set POProcess = Server.CreateObject("ADODB.Recordset")
POProcess.ActiveConnection = MM_INVFlood_STRING
POProcess.Source = "SELECT TOP 6 LEFT(CONVERT(varchar, PO_date,112),6) as POMonth, avg(datediff(d,[PO_DATE],[BILLIN])) as SendDay, avg(datediff(d,[BILLIN],[BillOUT])) as DocDay, avg(datediff(d,[BILLOUT],[BillEND])) as AccDay from MS_PO WHERE LEFT(CONVERT(varchar, PO_date,112),6)<='" & M_STOCK("YEAR") & M_STOCK("MONTH") & "' group by LEFT(CONVERT(varchar, PO_date,112),6) order by LEFT(CONVERT(varchar, PO_date,112),6)  desc"
POProcess.CursorType = 3
POProcess.CursorLocation = 3
POProcess.LockType = 1
POProcess.Open()

POProcess_numRows = 0



Set AGYear = Server.CreateObject("ADODB.Recordset")
AGYear.ActiveConnection = MM_INVFlood_STRING
AGYear.Source = "select top 5 agreeyear, count(Agreement) as noag, sum((AgreeQty/PACK_RATIO)*UnitPrice) as sumag, (select count(Agreement) as noag from Agreement ag where ag.BuyQty < ag.AgreeQty and Expdate>getdate() and ag.agreeyear=agmt.AgreeYear) as remainAg from Agreement agmt GROUP BY AgreeYear order by AgreeYear desc"
AGYear.CursorType = 3
AGYear.CursorLocation = 3
AGYear.LockType = 1
AGYear.Open()


Set RAG = Server.CreateObject("ADODB.Recordset")
RAG.ActiveConnection = MM_INVFlood_STRING
RAG.Source = "select Agreement.Agreement, Agreement.Expdate, INV_MD.DRUG_NAME, COMPANY.COMPANY_NAME, TBLBUY.BUYNAME, (AgreeQty/PACK_RATIO)*UnitPrice as sumag, (BuyQty/PACK_RATIO)*UnitPrice as sumpo, ((BuyQty/PACK_RATIO)*UnitPrice*100)/((AgreeQty/PACK_RATIO)*UnitPrice) as perbuy FROM Agreement " & _
			"INNER JOIN COMPANY ON Agreement.Company=COMPANY.COMPANY_CODE INNER JOIN TBLBUY ON Agreement.BuyMethod=TBLBUY.BUYCODE INNER JOIN INV_MD ON Agreement.WORKING_CODE=INV_MD.WORKING_CODE " & _
			"where agreement.BuyQty < agreement.AgreeQty and Expdate>getdate() order by ((BuyQty/PACK_RATIO)*UnitPrice*100)/((AgreeQty/PACK_RATIO)*UnitPrice) desc"
RAG.CursorType = 3
RAG.CursorLocation = 3
RAG.LockType = 1
RAG.Open()

POProcess_numRows = 0

Dim Repeat1__numRows
Dim Repeat1__index

Repeat1__numRows = -1
Repeat1__index = 0
Drug_numRows = Drug_numRows + Repeat1__numRows


Dim Repeat2__numRows
Dim Repeat2__index

Repeat2__numRows = -1
Repeat2__index = 0
SUBDrug_numRows = SUBDrug_numRows + Repeat2__numRows


Dim Repeat3__numRows
Dim Repeat3__index

Repeat3__numRows = -1
Repeat3__index = 0
Countdrug_numRows = Countdrug_numRows + Repeat3__numRows
%>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<!--meta http-equiv="refresh" content="30"-->
<title>รายงานการบริหารเวชภัณฑ์ งานเภสัชกรรม รพ.สต.บ้านกอสะเลียม</title>

<link rel="stylesheet" href="css/bootstrap.min.css"> 
<script src="js/jquery-3.2.1.min.js"></script>
<script src="js/bootstrap.min.js"></script>

    <script src="js/html5shiv.min.js"></script>
     <script src="js/respond.min.js"></script>

<style type="text/css">

</style>

<!--#include file="menu.asp" -->
</head>

<body>


<div class="container">

<h3><div class="text-primary" align="center"> รายงานการบริหารเวชภัณฑ์ งานเภสัชกรรม รพ.สต.บ้านกอสะเลียม</div></h3>

  <div class="row">
  
<br>

  
    <div class="col-md-6" >
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>มูลค่า&#3618;&#3634;&#3649;&#3621;&#3632;&#3648;&#3623;&#3594;&#3616;&#3633;&#3603;&#3601;&#3660;&#3588;&#3591;ค&#3621;&#3633;&#3591; ณ วันที่ <%=date()%> เวลา <%=time()%></strong></div>



<!--------------------------------------------------------------------------------------------------------------------------------------------------------------------------->

<table width="100%" class="table">
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
    <td width="48%" bgcolor="#FFFF66"><div align="center" class="style12"> <strong>
      <%response.write (formatnumber(totalvalue,2))%>
    </strong></div></td>
  </tr>
</table>

        <!--div class="panel-footer">Footer</div-->
      </div>
    </div>


<!--------------------------------------------------------------------------------------------------------------------------------------------------------------------------->

 
    <div class="col-md-6">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>ตัวชี้วัดสำคัญ</strong></div>
    
    
<table width="100%" class="table">
  <tr>
    <td width="52%" bgcolor="#33FFFF"><div align="center">ตัวชี้วัด</div></td>
    <td width="48%" bgcolor="#33FFFF"><div align="center">
 ผลการดำเนินงาน</div></td>
  </tr>

      <td bgcolor="#CCFFFF"><span class="style15">จำนวนเดือนสำรองคลัง (ข้อมูลเดือน <%=M_STOCK.Fields.Item("MONTH").Value%> ปี <%=M_STOCK.Fields.Item("YEAR").Value + 543%>)</span></td>
      <td bgcolor="#CCFFFF"><div align="center" class="style15"><%= formatnumber(CDbl(REMAIN_STOCK.Fields.Item("M_REMAIN").Value) / CDbl(M_STOCK.Fields.Item("SumOfSALE_VALUE").Value),2,-1)%> เดือน </div>		</td>
      <tr>
        <td bgcolor="#CCFFFF">สัดส่วนรายการยา (ED:NED)</td>
        <td bgcolor="#CCFFFF"><div align="center"><span class="style15"><%= formatnumber(CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value),0)%> : <%= formatnumber(CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value),0)%> (<%= formatnumber(CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value) / (CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value) + CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value))*100,2)%> : <%= formatnumber(CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value) / (CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value) + CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value))*100,2)%>) รวม  <%= formatnumber(CDbl(ITEM_VALUE1.Fields.Item("ITEM").Value) + CDbl(ITEM_VALUE2.Fields.Item("ITEM").Value),0)%> รายการ </span></div></td>
  <tr>
    <td bgcolor="#CCFFFF">สัดส่วนมูลค่ายา (ED:NED)</td>
        <td bgcolor="#CCFFFF"><div align="center"><span class="style15"><%= formatnumber(CDbl(ITEM_VALUE1.Fields.Item("VALUE").Value) / (CDbl(ITEM_VALUE1.Fields.Item("VALUE").Value) + CDbl(ITEM_VALUE2.Fields.Item("VALUE").Value))*100,2)%> : <%= formatnumber(CDbl(ITEM_VALUE2.Fields.Item("VALUE").Value) / (CDbl(ITEM_VALUE1.Fields.Item("VALUE").Value) + CDbl(ITEM_VALUE2.Fields.Item("VALUE").Value))*100,2)%></span></div></td>
  </table>

    <!--div class="panel-body flex-grow">Content here -- div with .flex-grow</div>
        <div class="panel-footer">Footer</div-->
      </div>
    </div>


</div>
<!--------------------------------------------------------------------------------------------------------------------------------------------------------------------------->
 <div class="row">

    <div class="col-md-6">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>รายงานสถานะใบสั่งซื้อ</strong></div>

<table width="100%" class="table">
  <tr>
    <td width="43%" bgcolor="#BCC2FA"><div align="center">สถานะใบสั่งซื้อ</div></td>
    <td width="25%" bgcolor="#BCC2FA"><div align="center">จำนวนใบสั่งซื้อ</div></td>
    <td width="32%" bgcolor="#BCC2FA"><div align="center">มูลค่า</div></td>
  </tr>
  <%  runno = 1
  		totalvalue = 0
		totalcost = 0 
	Y = right(budget.Fields.Item("year").Value,2)	
While ((Repeat1__numRows <> 0) AND (NOT POSTATUS.EOF)) 
%>
  <tr><td bgcolor="#DAE6FA"><%response.write(runno)%>
    . <%=(POSTATUS.Fields.Item("STATUSNAME").Value)%> </td>
      <td bgcolor="#DAE6FA"><div align="center"><a href="PO.asp?POstatus=<%=POSTATUS.Fields.Item("StatusCode").Value%>&YYY=<%=Y%>"><%=FORMATNUMBER(POSTATUS.Fields.Item("ITEM").Value,0)%></a></div></td>
      <td bgcolor="#DAE6FA"><div align="center"><a href="PO.asp?POstatus=<%=POSTATUS.Fields.Item("StatusCode").Value%>&amp;YYY=<%=Y%>"><%=FORMATNUMBER(POSTATUS.Fields.Item("TOTAL").Value,2)%></a></div>
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
    <td width="35%" bgcolor="#BCC2FA"><div align="center"><strong>รวม</strong></div></td>
    <td width="32%" bgcolor="#BCC2FA"><div align="center"><span class="style12"><strong>
      <%response.write (formatnumber(totalvalue,0))%>
    </strong></span></div></td>
    <td width="33%" bgcolor="#BCC2FA"><div align="center" class="style12"><strong>
      <%response.write (formatnumber(totalcost,2))%>
    </strong></div></td>
  </tr>
</table>
        <!--div class="panel-body flex-grow">Content here -- div with .flex-grow</div>
        <div class="panel-footer">Footer</div-->
      </div>
    </div>

<!--------------------------------------------------------------------------------------------------------------------------------------------------------------------------->


    <div class="col-md-6">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>รายงานการใช้งบจัดซื้อ ปีงบประมาณ <%=(budget.Fields.Item("year").Value)%></strong></div>

<table width="100%" class="table">
  <tr>
    <td width="24%" bgcolor="#FF99CC"><div align="center">ประเภทงบ</div></td>
    <td width="19%" bgcolor="#FF99CC"><div align="center">วงเงินที่ได้รับจัดสรร</div></td>
    <td width="19%" bgcolor="#FF99CC"><div align="center">หนี้สินผูกพัน</div></td>
    <td width="19%" bgcolor="#FF99CC"><div align="center">หนี้ 100% </div></td>
    <td width="19%" bgcolor="#FF99CC"><div align="center">วงเงินคงเหลือ</div></td>
  </tr>
  <%  runno = 1 
  t1 = 0
  t2 = 0
  t3=0
  t4 = 0
  
While ((Repeat1__numRows <> 0) AND (NOT Budget.EOF)) 
%>
  <tr>
    <td bgcolor="#FFCCFF"><%response.write(runno)%>
      . <%=Budget.Fields.Item("BDGNAME")%> </td>
    <td bgcolor="#FFCCFF"><div align="center"><%=formatnumber((Budget.Fields.Item("money").Value),2)%></div></td>
    <td bgcolor="#FFCCFF"><div align="center"><%=formatnumber((Budget.Fields.Item("deb1").Value),2)%></div></td>
    <td bgcolor="#FFCCFF"><div align="center"><%=formatnumber((Budget.Fields.Item("deb2").Value),2)%></div></td>
    <td bgcolor="#FFCCFF"><div align="center"><%=formatnumber(CDbl(Budget.Fields.Item("money").Value) - CDbl(Budget.Fields.Item("deb1").Value) - CDbl(Budget.Fields.Item("deb2").Value),2)%><span class="style2"> </span> </div></td>
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
    <td width="20%" bgcolor="#FF99CC"><div align="center"><strong>รวม</strong></div></td>
    <td width="20%" bgcolor="#FF99CC"><div align="center"><strong>
      <%response.write (formatnumber(t1,2))%>
    </strong></div></td>
    <td width="20%" bgcolor="#FF99CC"><div align="center"><strong>
      <%response.write (formatnumber(t2,2))%>
    </strong></div></td>
    <td width="20%" bgcolor="#FF99CC"><div align="center"><strong>
      <%response.write (formatnumber(t3,2))%>
    </strong></div></td>
    <td width="20%" bgcolor="#FF99CC"><div align="center"><strong>
        <%response.write (formatnumber(t4,2))%>
    </strong></div></td>
  </tr>
</table>
        <!--div class="panel-body flex-grow">Content here -- div with .flex-grow</div>
        <div class="panel-footer">Footer</div-->
      </div>
    </div>

</div>



 <div class="row">
 
 <div class="col-md-6">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>ระยะเวลาดำเนินการเฉลี่ยในแต่ละขั้นตอน</strong></div>

<table width="100%" class="table">
  <tr>
    <td width="15%" bgcolor="#EEAE93"><div align="center">เดือน</div></td>
    <td width="28%" bgcolor="#EEAE93"><div align="center">ระยะเวลาส่งของ</div></td>
    <td width="28%" bgcolor="#EEAE93"><div align="center">ระยะเวลาการจัดการเอกสาร</div></td>
    <td width="29%" bgcolor="#EEAE93"><div align="center">ระยะเวลาส่งตั้งเบิก</div></td>
  </tr>
  <%  runno = 1
	
While ((Repeat1__numRows <> 0) AND (NOT POProcess.EOF)) 
%>
  <tr><td bgcolor="#F7DEDB"><%response.write(runno)%>
    . <%=(POProcess.Fields.Item("POMonth").Value)%> </td>
      <td bgcolor="#F7DEDB"><div align="center"><%=POProcess.Fields.Item("SendDay").Value%></div></td>
      <td bgcolor="#F7DEDB"><div align="center"><%=POProcess.Fields.Item("DocDay").Value%></div></td>
      <td bgcolor="#F7DEDB"><div align="center"><%=POProcess.Fields.Item("AccDay").Value%></div></td>
  <tr>
    <%
	
  Repeat1__index=Repeat1__index+1
  at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1

  POProcess.MoveNext()
Wend
%>

</table>
        <!--div class="panel-body flex-grow">Content here -- div with .flex-grow</div>
        <div class="panel-footer">Footer</div-->
      </div>
    </div>
 
 
<!--------------------------------------------------------------------------------------------------------------------------------------------------------------------------->

    <div class="col-md-6">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>จำนวนการทำสัญญา</strong></div>

<table width="100%" class="table">
  <tr>
    <td width="43%" bgcolor="#BCC2FA"><div align="center">ปีงบประมาณ</div></td>
    <td width="25%" bgcolor="#BCC2FA"><div align="center">จำนวนสัญญาทั้งหมด</div></td>
	<td width="25%" bgcolor="#BCC2FA"><div align="center">ยังไม่ครบสัญญา</div></td>
    <td width="32%" bgcolor="#BCC2FA"><div align="center">รวมมูลค่าสัญญา</div></td>
  </tr>
  <%  
  
  

		
While NOT AGYear.EOF
%>
  <tr>
	<td bgcolor="#DAE6FA"><%=(AGYear.Fields.Item("agreeyear").Value)%> </td>
    <td bgcolor="#DAE6FA"><div align="center"><%=FORMATNUMBER(AGYear.Fields.Item("noag").Value,0)%></div></td>
	<td bgcolor="#DAE6FA"><div align="center"><%=FORMATNUMBER(AGYear.Fields.Item("remainAg").Value,0)%></div></td>
    <td bgcolor="#DAE6FA"><div align="center"><%=FORMATNUMBER(AGYear.Fields.Item("Sumag").Value,2)%></div>
          <div align="center"><span class="style2"> </span>
              <div align="left">
                <div align="center"></div>
              </div>
          </div></td>
  <tr>
    <%
	

  AGYear.MoveNext()
Wend
%>
  </tr>

</table>
        <!--div class="panel-body flex-grow">Content here -- div with .flex-grow</div>
        <div class="panel-footer">Footer</div-->
      </div>
    </div>




    

</div>


<!--------------------------------------------------------------------------------------------------------------------------------------------------------------------------->

 <div class="row">

    <div class="col-md-6">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>มูลค่ายาและเวชภัณฑ์คงคลัง ณ คลังยาย่อย</strong></div>


<table width="100%" class="table">
  <tr>
    <td width="52%" bgcolor="#33FF66"><div align="center">คลังยา</div></td>
    <td width="48%" bgcolor="#33FF66"><div align="center">
 มูลค่าคงคลัง
    </div></td>
  </tr>
  <%  runno = 1
  		totalvalue = 0
While ((Repeat2__numRows <> 0) AND (NOT SUBDrug.EOF)) 
%>
  <td bgcolor="#CCFFCC"><%response.write(runno)%>
    . <%=(SUBDrug.Fields.Item("DEPT_NAME").Value)%> </td>
      <td bgcolor="#CCFFCC"><div align="center">
	  <%=formatnumber((SUBDrug.Fields.Item("Sumtotal").Value),2)%> </div> 
          <div align="center"><span class="style2"> </span>
              <div align="left">
                <div align="center"></div>
              </div>
          </div></td>
  <tr>
    <%
	
  Repeat2__index=Repeat2__index+1
  at2__index=Repeat2__index+1
  Repeat2__numRows=Repeat2__numRows-1
  runno = runno +1
  totalvalue = totalvalue + CDbl("0"&SUBDrug.Fields.Item("Sumtotal").Value)
  SUBDrug.MoveNext()
Wend
%>
  </tr>
  <tr>
    <td width="52%" bgcolor="#33FF66"><div align="center"><strong>รวม</strong></div></td>
    <td width="48%" bgcolor="#33FF66"><div align="center" class="style12"><strong>
      <%response.write (formatnumber(totalvalue,2))%>
    </strong></div></td>
  </tr>
</table>

        <!--div class="panel-body flex-grow">Content here -- div with .flex-grow</div>
        <div class="panel-footer">Footer</div-->
      </div>
    </div>

<!--------------------------------------------------------------------------------------------------------------------------------------------------------------------------->
  
    <div class="col-md-6">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>สถานะของสัญญาที่ยังไม่ครบสัญญา</strong></div>

<table width="100%" class="table">
  <tr>
    <td width="20%" bgcolor="#EEAE93"><div align="center">สัญญาเลขที่</div></td>
    <td width="40%" bgcolor="#EEAE93"><div align="center">ชื่อยา</div></td>
    <td width="20%" bgcolor="#EEAE93"><div align="center">วันที่ครบสัญญา</div></td>
    <td width="20%" bgcolor="#EEAE93"><div align="center">ร้อยละการจัดซื้อ</div></td>
  </tr>
  <%  runno = 1
	
While NOT RAG.EOF
%>
  <tr><td bgcolor="#F7DEDB"><%response.write(runno)%>
    . <%=(RAG.Fields.Item("Agreement").Value)%> </td>
      <td bgcolor="#F7DEDB"><div align="left"><%=RAG.Fields.Item("DRUG_NAME").Value%></div></td>
      <td bgcolor="#F7DEDB"><div align="center"><%=RAG.Fields.Item("Expdate").Value%></div></td>
      <td bgcolor="#F7DEDB"><div align="center"><%=FORMATNUMBER(RAG.Fields.Item("perbuy").Value,2)%></div></td>
  <tr>
    <%
	
  runno = runno +1

  RAG.MoveNext()
Wend
%>

</table>
        <!--div class="panel-body flex-grow">Content here -- div with .flex-grow</div>
        <div class="panel-footer">Footer</div-->
      </div>
    </div>

</div>


</div> <!--containner-->
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