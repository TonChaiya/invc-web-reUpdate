<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Budget = request.querystring("FiscalYear")

sql = "SELECT TOP 1 MBS_RE_M.YEAR, MBS_RE_M.MONTH, Sum(MBS_RE_M.SALE_VALUE) AS SumOfSALE_VALUE, Sum(MBS_RE_M.REMAIN_VALUE) AS SumOfREMAIN_VALUE FROM MBS_RE_M GROUP BY MBS_RE_M.YEAR, MBS_RE_M.MONTH ORDER BY MBS_RE_M.YEAR DESC , MBS_RE_M.MONTH DESC;"
Set M_STOCK = Server.CreateObject("ADODB.Recordset")
M_STOCK.Open sql, Conn, 1,3

sql = "SELECT SUM(MNTH_SUM.TOTAL_VALUE) AS M_REMAIN FROM MNTH_SUM WHERE MNTH_SUM.YEAR='" & M_STOCK("YEAR") & "' AND MNTH_SUM.MONTH ='" & M_STOCK("MONTH") & "'"
Set REMAIN_STOCK = Server.CreateObject("ADODB.Recordset")
REMAIN_STOCK.Open sql, Conn, 1,3

sql = "SELECT Count(INV_MD.WORKING_CODE) AS ITEM, INV_MD.ED_NED, Sum(INV_MD.TOTAL_VALUE) AS VALUE FROM INV_MD GROUP BY INV_MD.ED_NED, INV_MD.NOUSE, INV_MD.OUT_OF_LIST, INV_MD.PO_INDIVIDUAL HAVING (((INV_MD.ED_NED)='1') AND ((INV_MD.NOUSE) Is Null) AND ((INV_MD.OUT_OF_LIST) Is Null) AND ((INV_MD.PO_INDIVIDUAL) Is Null));"
Set ITEM_VALUE1 = Server.CreateObject("ADODB.Recordset")
ITEM_VALUE1.Open sql, Conn, 1,3

sql = "SELECT Count(INV_MD.WORKING_CODE) AS ITEM, INV_MD.ED_NED, Sum(INV_MD.TOTAL_VALUE) AS VALUE FROM INV_MD GROUP BY INV_MD.ED_NED, INV_MD.NOUSE, INV_MD.OUT_OF_LIST, INV_MD.PO_INDIVIDUAL HAVING (((INV_MD.ED_NED)='2') AND ((INV_MD.NOUSE) Is Null) AND ((INV_MD.OUT_OF_LIST) Is Null) AND ((INV_MD.PO_INDIVIDUAL) Is Null));"
Set ITEM_VALUE2 = Server.CreateObject("ADODB.Recordset")
ITEM_VALUE2.Open sql, Conn, 1,3

Function CVNum2P(NUM,Dec)

	If IsNull(NUM) = True Or NUM = "" Then
		CVNum2P = 0
	Else
		If Dec ="Y" Then
			CVNum2P = formatnumber(NUM)
		else
			CVNum2P = formatnumber(NUM,"0,000")
		End If
	End If

End Function


Response.charset="windows-874"
%>




<table class="table table-bordered table-sm" width="100%" cellspacing="0">
  <thead>
	<tr>
		<td><div align="center">ตัวชี้วัด</div></td>
		<td><div align="center"> ผลการดำเนินงาน</div></td>
	</tr>
  </thead>
  <tbody>
	
	<tr>
      <td><span class="style15">จำนวนเดือนสำรองคลัง (ข้อมูลเดือน <%=M_STOCK("MONTH")%> ปี <%=M_STOCK("YEAR") + 543%>)</span></td>
      <td><div align="center" class="style15"><%= formatnumber(CDbl(REMAIN_STOCK("M_REMAIN")) / CDbl(M_STOCK("SumOfSALE_VALUE")),2,-1)%> เดือน </div>		</td>
    </tr>  
	<tr>
        <td>สัดส่วนรายการยา (ED:NED)</td>
        <td><div align="center"><span class="style15"><%= formatnumber(CDbl(ITEM_VALUE1("ITEM")),0)%> : <%= formatnumber(CDbl(ITEM_VALUE2("ITEM")),0)%> (<%= formatnumber(CDbl(ITEM_VALUE1("ITEM")) / (CDbl(ITEM_VALUE1("ITEM")) + CDbl(ITEM_VALUE2("ITEM")))*100,2)%> : <%= formatnumber(CDbl(ITEM_VALUE2("ITEM")) / (CDbl(ITEM_VALUE1("ITEM")) + CDbl(ITEM_VALUE2("ITEM")))*100,2)%>) รวม  <%= formatnumber(CDbl(ITEM_VALUE1("ITEM")) + CDbl(ITEM_VALUE2("ITEM")),0)%> รายการ </span></div></td>
	</tr>
	<tr>
		<td>สัดส่วนมูลค่ายา (ED:NED)</td>
		<td><div align="center"><span class="style15"><%= formatnumber(CDbl(ITEM_VALUE1("VALUE")) / (CDbl(ITEM_VALUE1("VALUE")) + CDbl(ITEM_VALUE2("VALUE")))*100,2)%> : <%= formatnumber(CDbl(ITEM_VALUE2("VALUE")) / (CDbl(ITEM_VALUE1("VALUE")) + CDbl(ITEM_VALUE2("VALUE")))*100,2)%></span></div></td>
	</tr>
	</tbody>
</table>