<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

WORKING_CODE = request.querystring("WORKING_CODE")
'WORKING_CODE = "2010930"
sql = "select top 24 m.WORKING_CODE, DRUG_NAME, DOSAGE_FORM,STD_RATIO3,QTY_ON_HAND/std_ratio3 as REMAIN, sum(ACTIVE_QTY1+ACTIVE_QTY2+ACTIVE_QTY3) As SALE_QUAN, sum(c.[VALUE]) as SALE_VALUE, ABC,VEN, left(convert(CHAR(8),OPERATE_DATE,112),6) + 54300 as DMonth from CARD c INNER JOIN INV_MD m ON c.WORKING_CODE=m.WORKING_CODE AND R_S_STATUS ='S' WHERE c.WORKING_CODE ='" & WORKING_CODE & "' group by m.WORKING_CODE, DRUG_NAME, DOSAGE_FORM,STD_RATIO3,QTY_ON_HAND/std_ratio3,ABC,VEN, left(convert(CHAR(8),OPERATE_DATE,112),6) + 54300 order by left(convert(CHAR(8),OPERATE_DATE,112),6) + 54300 desc"

Set PO = Server.CreateObject("ADODB.Recordset")
PO.Open sql, Conn, 1,3

' if PO.EOF then 

	' sql = "select m.WORKING_CODE, DRUG_NAME, DOSAGE_FORM,STD_RATIO3,QTY_ON_HAND/std_ratio3 as REMAIN, SUM(ACTIVE_QTY1+ACTIVE_QTY2+ACTIVE_QTY3) As SALE_QUAN, Sum(c.[VALUE]) as SALE_VALUE, ABC,VEN from CARD c INNER JOIN INV_MD m ON c.WORKING_CODE=m.WORKING_CODE AND R_S_STATUS ='S' WHERE case 	when month(OPERATE_DATE)>=10 then Year(OPERATE_DATE)+543+1 else Year(OPERATE_DATE)+543 end ='" & Budget & "' group by m.WORKING_CODE, DRUG_NAME, DOSAGE_FORM,STD_RATIO3,QTY_ON_HAND/std_ratio3,ABC,VEN order by Sum(c.[VALUE]) desc"
	' Set PO = Server.CreateObject("ADODB.Recordset")
	' PO.Open sql, Conn, 1,3

' end if 

Response.charset="windows-874"
%>
<script>

function ShowSearchItem(){
	$("#SearchItemModal").modal('show');
}

</script>

<div class="content-wrapper">
    <div class="container-fluid">
		
		<br>
		<div class="row">
		
			<div class="col-md-6" >
				<div class="card mb-3">
					<div class="card-header" ><i class="fa fa-area-chart"></i><a name="ASU"></a> กราฟ</div>
						<div class="card-body">								
							<div class="panel panel-default flex-col">
								<div class="panel-heading"><strong></strong>			
									<div id="chartContainer" style="height: 300px; width: 100%;"></div>
								</div>	
							</div>
						</div>
				</div>
			</div>
<script>
	callChart();
</script>
		<!--------------------------------------------------------------------------------------------------------------------------------------------------------------------------->
		 
			<div class="col-md-6" >
				<div class="card mb-3">
					<div class="card-header" ><i class="fa fa-area-chart"></i><a name="ASU"></a> ตาราง</div>
						<div class="card-body">
			
							<div class="panel panel-default flex-col">
								<div class="panel-heading"><strong></strong>			
									<%if not PO.EOF then%>
									<table class="table table-bordered table-sm" width="100%" cellspacing="0">
										<tr>
											<td>รหัส</td>
											<td><%=PO("WORKING_CODE")%></td>
											<td>ชื่อ</td>
											<td><%=PO("DRUG_NAME")%></td>
											<td>รูปแบบ</td>
											<td><%=PO("DOSAGE_FORM")%></td>
										</tr>
										<tr>
											<td>ขนาดบรรจุ</td>
											<td><%=PO("STD_RATIO3")%></td>
											<td>ABC</td>
											<td><%=PO("ABC")%></td>
											<td>VEN</td>
											<td><%=PO("VEN")%></td>
										</tr>
										
									</table>

									<table class="table table-bordered table-sm" width="100%" cellspacing="0">
									  <thead>
										  <tr>
											<th><div align="center">เดือน</div></th>
											<th><div align="center">จำนวนใช้</div></th>
											<th><div align="center">มูลค่า</div></th>
											<th><div align="center">ส่วนต่าง</div></th>
										  </tr>
									  </thead>
									  <tbody>
									<%  runno = 1
									While NOT PO.EOF
									%>
										<tr> 
											<td><div align="center"><%=PO("DMonth")%></div></td>
											<td><div align="center"><%=PO("SALE_QUAN")%></div></td>
											<td><div align="left"><%=(PO("SALE_VALUE"))%></div></td>
											<td><div align="center">
											<%
											if runno>1 then 
												response.write(csng(PO("SALE_VALUE"))-csng(temp_salevalue))
											end if 
											%>
											</div></td>
										<tr>
									   
									<%
									  temp_salevalue = PO("SALE_VALUE")
									  runno = runno +1
									  PO.MoveNext()
									Wend
									%>
										</tbody>
									</table>

									<%end if%>
									
								</div>	
							</div>
						</div>
					
				</div>
			</div>
			
		</div>
	
	</div>
</div>

<!-- Search Item Modal-->
    <div class="modal fade" id="SearchItemModal" tabindex="-1" role="dialog" aria-labelledby="SearchItemModal" aria-hidden="true">
      <div class="modal-dialog modal-lg" role="document">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title" id="exampleModalLabel">ค้นหารายการยาและเวชภัณฑ์</h5>
            <button class="close" type="button" data-dismiss="modal" aria-label="Close">
              <span aria-hidden="true">x</span>
            </button>
          </div>
		  <div class="modal-body">
			<input type="text" id="ItemSearchKeyword" class="form-control" placeholder="----- ค้นหา -----" onKeyUP="if(event.keyCode==13) { searchItem(this.value) };">
			<div id="ItemSearchDiv"></div>
		  </div>
          
          <div class="modal-footer">
            <button class="btn btn-secondary" type="button" data-dismiss="modal">Close</button>
          </div>
        </div>
      </div>
    </div>