<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Budget = request.querystring("FiscalYear")
flag = request.querystring("flag")

sql = "SELECT BDG_TYPE.BDGNAME, BUDGET.money, BUDGET.deb1, BUDGET.deb2, BUDGET.year, BUDGET.BudgetOpen FROM BDG_TYPE INNER JOIN BUDGET ON BDG_TYPE.BDGCODE = BUDGET.type WHERE BUDGET.[year]='" & Budget & "'"
Set rsBudget = Server.CreateObject("ADODB.Recordset")
rsBudget.Open sql, Conn, 1,3

sql = "select * From BUDGET_ORIGIN o inner join BDG_TYPE t on o.type = t.BDGCODE inner join  BDG_SOURCE s on o.source = s.B_SOURCE WHERE o.[year]='" & Budget & "' order by o.date"
Set rsOrigin = Server.CreateObject("ADODB.Recordset")
rsOrigin.Open sql, Conn, 1,3


Response.charset="windows-874"
%>


<div class="row">


    <div class="col-md-6">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>รายงานการใช้งบจัดซื้อ ปีงบประมาณ  <%=(rsBudget("year"))%></strong></div>

			<table width="100%" class="table">
			  <tr>
				<td><div align="center">ประเภทงบ</div></td>
				<td><div align="center">วงเงินที่ได้รับจัดสรร</div></td>
				<td><div align="center">หนี้สินผูกพัน</div></td>
				<td><div align="center">หนี้ 100% </div></td>
				<td><div align="center">วงเงินคงเหลือ</div></td>
			  </tr>
			  <%  runno = 1 
			  t1 = 0
			  t2 = 0
			  t3=0
			  t4 = 0
			  
			While NOT rsBudget.EOF
			%>
			  <tr>
				<td><%response.write(runno)%>.<%=rsBudget("BDGNAME")%> </td>
				<td><div align="center"><%=formatnumber(rsBudget("money"),2)%></div></td>
				<td><div align="center"><%=formatnumber(rsBudget("deb1"),2)%></div></td>
				<td><div align="center"><%=formatnumber(rsBudget("deb2"),2)%></div></td>
				<td><div align="center"><%=formatnumber(CDbl(rsBudget("money")) - CDbl(rsBudget("deb1")) - CDbl(rsBudget("deb2")),2)%><span class="style2"> </span> </div></td>
			  </tr>
			  <tr>
				<%

			  runno = runno +1 
			  t1 = t1 + CDbl(rsBudget("money")) 
			  t2 = t2 +  CDbl(rsBudget("deb1")) 
			  t3 = t3 +  CDbl(rsBudget("deb2"))
			  t4 = t4 +  (CDbl(rsBudget("money")) - CDbl(rsBudget("deb1")) - CDbl(rsBudget("deb2")))
			  rsBudget.MoveNext()
			Wend
			%>
			  </tr>
			  <tr>
				<td><div align="center"><strong>รวม</strong></div></td>
				<td><div align="center"><strong>
				  <%response.write (formatnumber(t1,2))%>
				</strong></div></td>
				<td><div align="center"><strong>
				  <%response.write (formatnumber(t2,2))%>
				</strong></div></td>
				<td><div align="center"><strong>
				  <%response.write (formatnumber(t3,2))%>
				</strong></div></td>
				<td><div align="center"><strong>
					<%response.write (formatnumber(t4,2))%>
				</strong></div></td>
			  </tr>
			</table>
		</div>
    </div>
	
	<div class="col-md-6">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>ที่มาของงบจัดซื้อยาและเวชภัณฑ์ ปีงบประมาณ  <%=(rsOrigin("year"))%></strong></div>

			<table width="100%" class="table">
			  <tr>
				<td><div align="center">วันที่ได้รับงบประมาณ</div></td>
				<td><div align="center">ที่มาของงบประมาณ</div></td>
				<td><div align="center">ประเภทงบ</div></td>
				<td><div align="center">วงเงินที่ได้รับจัดสรร</div></td>
				
			  </tr>
			  <%  runno = 1 
			  t1 = 0
			  t2 = 0
			  t3=0
			  t4 = 0
			  
			While NOT rsOrigin.EOF
			%>
			  <tr>
				<td><%=rsOrigin("date")%> </td>
				<td><div align="center"><%=rsOrigin("B_SOURCE_NAME")%></div></td>
				<td><div align="center"><%=rsOrigin("BDGNAME")%></div></td>
				<td><div align="center"><%=formatnumber(rsOrigin("money"),2)%></div></td>
				
			  </tr>
			  <tr>
				<%

			  runno = runno +1 
			  t1 = t1 + CDbl(rsOrigin("money")) 
			  
			  rsOrigin.MoveNext()
			Wend
			%>
			  </tr>
			  <tr>
				<td colspan ="3"><div align="center"><strong>รวม</strong></div></td>
				
				<td><div align="center"><strong>
					<%response.write (formatnumber(t1,2))%>
				</strong></div></td>
			  </tr>
			</table>
		</div>
    </div>
	
</div>