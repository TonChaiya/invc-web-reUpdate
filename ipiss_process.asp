<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Budget = request.querystring("FiscalYear")
flag = request.querystring("flag")

sql = "SELECT  TblPOStatus.StatusCode, TblPOStatus.StatusName, Count(MS_PO.PO_NO) AS ITEM, Left([PO_NO],2) AS Expr1, sum([TOTAL_COST]) as Total FROM TblPOStatus INNER JOIN MS_PO ON TblPOStatus.StatusCode = MS_PO.STATUS GROUP BY  TblPOStatus.StatusCode, TblPOStatus.StatusName, Left([PO_NO],2) HAVING (((Left([PO_NO],2))='" &  right(budget,2) & "')) ORDER BY TblPOStatus.StatusCode"
Set rsStatus = Server.CreateObject("ADODB.Recordset")
rsStatus.Open sql, Conn, 1,3

sql = "SELECT TOP 6 LEFT(CONVERT(varchar, PO_date,112),6) as POMonth, avg(datediff(d,[PO_DATE],[BILLIN])) as SendDay, avg(datediff(d,[BILLIN],[BillOUT])) as DocDay, avg(datediff(d,[BILLOUT],[BillEND])) as AccDay from MS_PO WHERE LEFT(PO_NO,2)<='" & right(budget,2) & "' group by LEFT(CONVERT(varchar, PO_date,112),6) order by LEFT(CONVERT(varchar, PO_date,112),6)  desc"
Set rsProcess = Server.CreateObject("ADODB.Recordset")
rsProcess.Open sql, Conn, 1,3


Response.charset="windows-874"
%>


<div class="row">


    <div class="col-md-6">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>สถานะใบสั่งซือ</strong></div>

			<table width="100%" class="table">
			  <tr>
				<td><div align="center">สถานะใบสั่งซื้อ</div></td>
				<td><div align="center">จำนวนใบสั่งซื้อ</div></td>
				<td><div align="center">มูลค่า</div></td>
				
			  </tr>
			  <%  runno = 1 
			  t1 = 0
			  t2 = 0
			  
			While NOT rsStatus.EOF
			%>
			  <tr>
				<td><%response.write(runno)%>.<%=rsStatus("StatusName")%> </td>
				<td><div align="center"><%=formatnumber(rsStatus("ITEM"),0)%></div></td>
				<td><div align="center"><%=formatnumber(rsStatus("Total"),2)%></div></td>
			  </tr>
			  <tr>
				<%

			  runno = runno +1 
			  t1 = t1 + CDbl(rsStatus("ITEM")) 
			  t2 = t2 +  CDbl(rsStatus("Total")) 
			  
			  rsStatus.MoveNext()
			Wend
			%>
			  </tr>
			  <tr>
				<td><div align="center"><strong>รวม</strong></div></td>
				<td><div align="center"><strong>
				  <%response.write (formatnumber(t1,0))%>
				</strong></div></td>
				<td><div align="center"><strong>
				  <%response.write (formatnumber(t2,2))%>
				</strong></div></td>
			  </tr>
			</table>
		</div>
    </div>
	
	<div class="col-md-6">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>ระยะเวลาดำเนินการเฉลี่ยในแต่ละขั้นตอน</strong></div>

			<table width="100%" class="table">
			  <tr>
				<td><div align="center">เดือน</div></td>
				<td><div align="center">ระยะเวลาส่งของ</div></td>
				<td><div align="center">ระยะเวลาจัดการเอกสาร</div></td>
				<td><div align="center">ระยะเวลาส่งตั้งเบิก</div></td>

			  </tr>
			<%  runno = 1   
			While NOT rsProcess.EOF
			%>
			  <tr>
				<td><div align="center"><%=rsProcess("POMonth")%></div></td>
				<td><div align="center"><%=rsProcess("SendDay")%></div></td>
				<td><div align="center"><%=rsProcess("DocDay")%></div></td>
				<td><div align="center"><%=rsProcess("AccDay")%></div></td>
				
			  </tr>
			  
				<%
			  runno = runno +1 
			  rsProcess.MoveNext()
			Wend
			%>
			

			</table>
		</div>
    </div>
	
</div>