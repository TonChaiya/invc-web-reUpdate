<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->


<%
mode = request.querystring("mode")

SUB_PO_NO = request.form("SUB_PO_NO")
SUB_PO_NO = request.querystring("SUB_PO_NO")

sql = "select s.SUB_PO_NO, s.SUB_PO_DATE, s.DEPT_ID, d.DEPT_NAME, c.WORKING_CODE, m.DRUG_NAME, c.QTY_RCV, c.QTY_ORDER, c.PACK_RATIO, c.QTY_RCV1, c.PACK_RATIO1, c.EXPIRED_DATE1,c.LOTNO1, c.LOCATION1, c.QTY_RCV2, c.PACK_RATIO2, c.EXPIRED_DATE2,c.LOTNO2, c.LOCATION2, c.QTY_RCV3, c.PACK_RATIO3, c.EXPIRED_DATE3,c.LOTNO3, c.LOCATION3 from SM_PO s inner JOIN  SM_PO_C c on s.SUB_PO_NO=c.SUB_PO_NO inner join INV_MD m on c.WORKING_CODE=m.WORKING_CODE inner join DEPT_ID d on s.DEPT_ID=d.DEPT_ID left join LOCATION l1 on c.location1 = l1.LOCATION_NAME left join LOCATION l2 on c.location2 = l2.LOCATION_NAME left join LOCATION l3 on c.location3 = l3.LOCATION_NAME where c.SUB_PO_NO='" & SUB_PO_NO & "' and (l1.LOCATION_GROUP ='06' or l2.LOCATION_GROUP ='06' or l3.LOCATION_GROUP ='06')" 

Set SMPOC = Server.CreateObject("ADODB.Recordset")
SMPOC.Open sql, Conn, 2,1

Response.charset="windows-874"
%>
<body style = "font-family:sans-serif;font-size:22px;" onload="setTimeout(window.print(), 500);">
<script>
              window.addEventListener('afterprint', (event) => {
                console.log('After print');
                window.close();
              });
</script>
  <div class="content-wrapper">
    <div class="container-fluid">
      <!-- Breadcrumbs-->
      <!--ol class="breadcrumb">
        <li class="breadcrumb-item">
          <a href="itemlist.asp">สถานะคงคลัง</a>
        </li>
        <li class="breadcrumb-item active"></li>
      </ol>
      <!-- Example DataTables Card-->

	<p class="font-weight-bold" align="center">ใบจัดเวชภัณฑ์ตามใบเบิก (งานผลิต)</p>
	<table class="table table-borderless table-sm" style="font-size:14px;">
		<tr>
			<td><div align="left">รหัสใบเบิก : </div></td>
			<td><div align="left"><%=SMPOC("SUB_PO_NO")%></div></td>
			<td><div align="left">วันที่เบิก : </div></td>
			<td><div align="left"><%=SMPOC("SUB_PO_DATE")%></div></td>
		</tr>
		<tr>
			<td><div align="left"> หน่วยเบิก : </div></td>
			<td colspan="3"><div align="left" class="font-weight-bold"><%=SMPOC("DEPT_NAME") & " (" & SMPOC("DEPT_ID") & ")"%></div></td>
		</tr>
	</table>
	
        <div class="card-body">
          <div style="font-size:14px;"> 
				<%  runno = 1
				While NOT SMPOC.EOF
				%>
				<table class="table table-borderless table-sm" style="font-size:14px;">	
					<tr> 
						<td><div align="center"><%=runno%>. </div></td>
						<td><div align="center"><%=SMPOC("WORKING_CODE")%></div></td>
						<td><div align="left"><%=(SMPOC("DRUG_NAME"))%></div></td>
						<td><div align="center"> จำนวน : <%=SMPOC("QTY_RCV")%></div></td>
					</tr>
					<tr>
						<td colspan="5">
							<table class="table table-borderless table-sm" style="font-size:14px;">
								<tr>
									<td><div align="center">ลำดับ</div></td>
									<td><div align="center">จำนวนจ่าย</div></td>
									<td><div align="center">วันหมดอายุ</div></td>
									<td><div align="center">เลขที่ผลิต</div></td>
									<td><div align="center">สถานที่เก็บ</div></td>
								</tr>

								<% runnoc = 1 
								if SMPOC("QTY_RCV1") > "0" then%>
									<tr>
										<td><div align="center"><%=runno & "." & runnoc%></div></td>
										<td><div align="center"><%=CSng(SMPOC("QTY_RCV1"))/Csng(SMPOC("PACK_RATIO1"))%> x <%=SMPOC("PACK_RATIO1")%></div></td>
										<td><div align="center"><%=SMPOC("EXPIRED_DATE1")%></div></td>
										<td><div align="center"><%=SMPOC("LOTNO1")%></div></td>
										<td><div align="center"><%=SMPOC("LOCATION1")%></div></td>
									</tr>
								<%runnoc = runnoc + 1 
								end if

								if SMPOC("QTY_RCV2") > "0" then%>
									<tr>
										<td><div align="center"><%=runno & "." & runnoc%></div></td>
										<td><div align="center"><%=CSng(SMPOC("QTY_RCV2"))/Csng(SMPOC("PACK_RATIO2"))%> x <%=SMPOC("PACK_RATIO2")%></div></td>
										<td><div align="center"><%=SMPOC("EXPIRED_DATE2")%></div></td>
										<td><div align="center"><%=SMPOC("LOTNO2")%></div></td>
										<td><div align="center"><%=SMPOC("LOCATION2")%></div></td>
									</tr>
								<%runnoc = runnoc + 1 
								end if

								if SMPOC("QTY_RCV3") > "0" then%>
									<tr>
										<td><div align="center"><%=runno & "." & runnoc%></div></td>
										<td><div align="center"><%=CSng(SMPOC("QTY_RCV3"))/Csng(SMPOC("PACK_RATIO3"))%> x <%=SMPOC("PACK_RATIO3")%></div></td>
										<td><div align="center"><%=SMPOC("EXPIRED_DATE3")%></div></td>
										<td><div align="center"><%=SMPOC("LOTNO3")%></div></td>
										<td><div align="center"><%=SMPOC("LOCATION3")%></div></td>
									</tr>
								<%end if%>
							</table>
						</td>
					</tr>
				<%
				
				runno = runno +1
				SMPOC.MoveNext()
				Wend

SMPOC.close 
set SMPOC = nothing 

%>

			</table>

</body>

