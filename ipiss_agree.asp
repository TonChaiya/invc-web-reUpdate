<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Budget = request.querystring("FiscalYear")
flag = request.querystring("flag")

sql = "select top 5 agreeyear, count(Agreement) as noag, sum((AgreeQty/PACK_RATIO)*UnitPrice) as sumag, (select count(Agreement) as noag from Agreement ag where ag.BuyQty < ag.AgreeQty and Expdate>getdate() and ag.agreeyear=agmt.AgreeYear) as remainAg from Agreement agmt GROUP BY AgreeYear order by AgreeYear desc"
Set rsAgree = Server.CreateObject("ADODB.Recordset")
rsAgree.Open sql, Conn, 1,3

sql = "select Agreement.Agreement, Agreement.Expdate, INV_MD.DRUG_NAME, COMPANY.COMPANY_NAME, TBLBUY.BUYNAME, (AgreeQty/PACK_RATIO)*UnitPrice as sumag, (BuyQty/PACK_RATIO)*UnitPrice as sumpo, ((BuyQty/PACK_RATIO)*UnitPrice*100)/((AgreeQty/PACK_RATIO)*UnitPrice) as perbuy FROM Agreement " & _
	  "INNER JOIN COMPANY ON Agreement.Company=COMPANY.COMPANY_CODE INNER JOIN TBLBUY ON Agreement.BuyMethod=TBLBUY.BUYCODE INNER JOIN INV_MD ON Agreement.WORKING_CODE=INV_MD.WORKING_CODE " & _
	  "where agreement.BuyQty < agreement.AgreeQty and Expdate>getdate() order by ((BuyQty/PACK_RATIO)*UnitPrice*100)/((AgreeQty/PACK_RATIO)*UnitPrice) desc"
Set rsAgreeDetail = Server.CreateObject("ADODB.Recordset")
rsAgreeDetail.Open sql, Conn, 1,3


Response.charset="windows-874"
%>


<div class="row">


    <div class="col-md-4">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>จำนวนการทำสัญญาในแต่ละปี</strong></div>

			<table width="100%" class="table">
			  <tr>
				<td><div align="center">ปีงบประมาณ</div></td>
				<td><div align="center">จำนวนสัญญาทั้งหมด</div></td>
				<td><div align="center">ยังไม่ครบสัญญา</div></td>
				<td><div align="center">รวมมูลค่าสัญญา</div></td>
				
			  </tr>
			<%  runno = 1   
			While NOT rsAgree.EOF
			%>
			  <tr>
				<td><div align="center"><%=rsAgree("agreeyear")%></div> </td>
				<td><div align="center"><%=formatnumber(rsAgree("noag"),0)%></div></td>
				<td><div align="center"><%=formatnumber(rsAgree("remainAg"),0)%></div></td>
				<td><div align="center"><%=formatnumber(rsAgree("sumag"),2)%></div></td>				
			  </tr>
			  <tr>
			<%
			  runno = runno +1 
			  rsAgree.MoveNext()
			Wend
			%>
			  </tr>
			</table>
		</div>
    </div>
	
	<div class="col-md-8">
      <div class="panel panel-default flex-col">
        <div class="panel-heading"><strong>สถานะของสัญญาที่ยังไม่ครบสัญญา</strong></div>

			<table width="100%" class="table">
			  <tr>
				<td><div align="center">สัญญาเลขที่</div></td>
				<td><div align="center">ขื่อยา/เวชภัณฑ์</div></td>
				<td><div align="center">บริษัทคู่สัญญา</div></td>
				<td><div align="center">วันที่ครบสัญญา</div></td>
				<td><div align="center">มูลค่าสัญญา</div></td>
				<td><div align="center">มูลค่าที่จัดซื้อแล้ว</div></td>
				<td><div align="center">ร้อยละการจัดซื้อ</div></td>
				
			  </tr>
			  <%  runno = 1 			  
			While NOT rsAgreeDetail.EOF
			%>
			  <tr>
				<td><div align="center"><%=rsAgreeDetail("agreement")%> </div></td>
				<td><div align="left"><%=rsAgreeDetail("DRUG_NAME")%></div></td>
				<td><div align="left"><%=rsAgreeDetail("COMPANY_NAME")%></div></td>
				<td><div align="right"><%=rsAgreeDetail("Expdate")%></div></td>
				<td><div align="right"><%=formatnumber(rsAgreeDetail("sumag"),2)%></div></td>
				<td><div align="right"><%=formatnumber(rsAgreeDetail("sumpo"),2)%></div></td>
				<td><div align="right"><%=formatnumber(rsAgreeDetail("perbuy"),2)%></div></td>
				
			  </tr>
			  <tr>
				<%

			  runno = runno +1 
			  rsAgreeDetail.MoveNext()
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