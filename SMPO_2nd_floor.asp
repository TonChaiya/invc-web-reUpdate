<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->


<%
mode = request.querystring("mode")
keyword = request.querystring("keyword")

sql = "select s.SUB_PO_NO, s.SUB_PO_DATE, s.ACC_NO, s.DEPT_ID, d.DEPT_NAME, count(c.WORKING_CODE) as NoOfItem, s.PROCESS from SM_PO s inner JOIN  SM_PO_C c on s.SUB_PO_NO=c.SUB_PO_NO inner join INV_MD m on c.WORKING_CODE=m.WORKING_CODE inner join DEPT_ID d on s.DEPT_ID=d.DEPT_ID left join LOCATION l1 on c.location1 = l1.LOCATION_NAME left join LOCATION l2 on c.location2 = l2.LOCATION_NAME left join LOCATION l3 on c.location3 = l3.LOCATION_NAME where s.SUB_PO_NO>'S6800001' and (l1.LOCATION_GROUP ='06' or l2.LOCATION_GROUP ='06' or l3.LOCATION_GROUP ='06') group by s.SUB_PO_NO, s.SUB_PO_DATE, s.ACC_NO, s.DEPT_ID, d.DEPT_NAME, s.PROCESS order by s.SUB_PO_NO desc" 


Set SMPO = Server.CreateObject("ADODB.Recordset")
SMPO.Open sql, Conn, 1,3


function ShowUserName(UserID)

	if isnull(UserID) = true or UserID = "" then 
		ShowUserName = "" 
		exit function
	end if 

	sql = "select [name] from [User] where UserID=" & UserID 
	Set Usr = Server.CreateObject("ADODB.Recordset")
	Usr.Open sql, Conn, 1,3

	if not Usr.eof then 
		ShowUserName = Usr("name")
	else 
		ShowUserName = ""
	end if
	Usr.close 
	set Usr = nothing

end function

Response.charset="windows-874"
%>

<!-- Bootstrap core CSS-->
  <link href="vendor/bootstrap/css/bootstrap.min.css" rel="stylesheet">
  <!-- Custom fonts for this template-->
  <link href="vendor/font-awesome/css/font-awesome.min.css" rel="stylesheet" type="text/css">
  <link href="font-awesome-5.12.1/css/all.css" rel="stylesheet" type="text/css">
  <!-- Page level plugin CSS-->
  <link href="vendor/datatables/dataTables.bootstrap4.css" rel="stylesheet">
  <!-- Custom styles for this template-->
  <link href="css/sb-admin.css" rel="stylesheet">
  
  <!-- Bootstrap core JavaScript-->
    <script src="vendor/jquery/jquery.min.js"></script>
    <script src="vendor/bootstrap/js/bootstrap.bundle.min.js"></script>
    <!-- Core plugin JavaScript-->
    <script src="vendor/jquery-easing/jquery.easing.min.js"></script>
    <!-- Page level plugin JavaScript-->
    <script src="vendor/datatables/jquery.dataTables.js"></script>
    <script src="vendor/datatables/dataTables.bootstrap4.js"></script>
    <!-- Custom scripts for all pages-->
    <script src="js/sb-admin.min.js"></script>
    <!-- Custom scripts for this page-->
    <script src="js/sb-admin-datatables.min.js"></script>


<script>

function ShowDetail(SUB_PO_NO){

	$('#SMPODetail').modal('show');

	$.post( "SMPOC.asp", { SUB_PO_NO:SUB_PO_NO } ).done(function( data ) {
		console.log (data);
		$('#ShowDetailSMPO').html(data);		
	})

}


function PrintSlip(SUB_PO_NO,Status){

	if (Status == 'Y'){ 
		$.post( "SavePrintSlip.asp", { SUB_PO_NO:SUB_PO_NO } ).done(function( data ) {
			
			if (data=='success'){
				//$('#S' + SUB_PO_NO).val('');	
				window.open("SMPOCPrint.asp?SUB_PO_NO=" + SUB_PO_NO);
			}else{
				alert('บันทึกไม่สำเร็จ');
			}
		})
   	} else {
		window.open("SMPOCPrint.asp?SUB_PO_NO=" + SUB_PO_NO);
	}
}



function SelectMode(Mode){
	
	<%keyword = ""%>
	window.open('EOC.asp?Mode=' + Mode,"_self");

}

function SearchKeyword(keyword){
	if (keyword=='') {
		var keyword = document.getElementById("keyword").value; 
	}
	window.open('EOC.asp?keyword=' + keyword,"_self");

}

function login(){
	window.open('index.html?FromPage=2ndFloor',"_self");
}

function logout(){
	
	$.post( "logout.asp", {FromPage:'2ndFloor'}).done(function( data ) {
		location.reload();
	})
}

</script>

<body class="fixed-nav sticky-footer bg-dark" id="page-top">

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
      <div class="card mb-3">
		<div class="card-header"> ระบบบริหารเวชภัณฑ์ INVC
			<table width="100%">
				<tr>
					<td align="left">
						<div>
							<i class="fa fa-table"></i> รายการใบเบิกของคลังใหญ่ที่ฝากยาเก็บไว้ที่อาคารเภสัชกรรม
						</div>
					</td>
					<td>
					</td>
					<td align="right">
						<div>
							<%if session("UserID") = "" then %>
								<button class="btn btn-success btn-sm" onclick="login()"><i class="fas fa-sign-in-alt"></i> login </button>
							<%else
								response.write (session("NameOfUser"))
							%>
								 <button class="btn btn-danger btn-sm" onclick="logout()"><i class="fas fa-sign-in-alt"></i> logout </button>
							<%end if%>
						</div>
					</td>
				</tr>
			</table>
		</div>
        <div class="card-body">
          <div class="table-responsive">
            <table class="table table-bordered table-sm" id="dataTable" width="100%" cellspacing="0" style="font-size:14px;">
              	<thead>
					<tr>
						<th><div align="center">ลำดับ</div></th>
						<th><div align="center">รหัสใบเบิก</div></th>
						<th><div align="center">วันที่เบิก</div></th>
						<th><div align="center">หน่วยเบิก</div></th>
						<th><div align="center">จำนวนรายการ</div></th>
						<th><div align="center">ดูรายละเอียด</div></th>
						<th><div align="center">พิมพ์ใบจัด</div></th>
						<th><div align="center">พิมพ์โดย</div></th>
					</tr>
				</thead>
				<tbody>
				<%  runno = 1
				While NOT SMPO.EOF
				%>
					
					<tr> 

						<td><div align="center"><%=runno%></div></td>
						<td><div align="center"><%=SMPO("SUB_PO_NO")%></div></td>
						<td><div align="center"><%=(SMPO("SUB_PO_DATE"))%></div></td>
						<td><div align="center"><%=SMPO("DEPT_ID") & "-" & SMPO("DEPT_NAME")%></div></td>
						<td><div align="center"><%=SMPO("NoOfItem")%></div></td>
						<td><div align="center"><button type="button" class="btn btn-primary btn-sm" onclick="ShowDetail('<%=SMPO("SUB_PO_NO")%>')"><i class="fas fa-list"></i> รายละเอียด</button></div></td>
						<td><div align="center">
							<%if SMPO("Process") = "A" then %>
								<%if session("UserID") <> "" then %>
									<% if isnull(SMPO("ACC_NO")) = true or SMPO("ACC_NO") = "" then %>
										<button type="button" class="btn btn-success btn-sm" onclick="PrintSlip('<%=SMPO("SUB_PO_NO")%>','Y')"><i class="fas fa-print"></i> พิมพ์ใบจัด </button>
									<%else%>
										<button type="button" class="btn btn-secondary btn-sm" onclick="PrintSlip('<%=SMPO("SUB_PO_NO")%>','N')"><i class="fas fa-print"></i> พิมพ์ใบจัด </button>
									<%end if%>
								<%end if%> 
							<%end if%> 
						</div></td>
						<td><div align="center"><%=ShowUserName(SMPO("ACC_NO"))%></div></td>
					<tr>
				
				<%
				
				runno = runno +1
				SMPO.MoveNext()
				Wend

SMPO.close 
set SMPO = nothing 

%>
				</tbody>
			</table>

          </div>
        </div>
        <div class="card-footer small text-muted">Your IP address : <%Response.Write(Request.ServerVariables("remote_addr"))%></div>
      </div>
    </div>


		<div class="modal fade" id="SMPODetail" tabindex="-1" role="dialog" aria-labelledby="SMPODetailModalLabel" aria-hidden="true" data-backdrop="static" data-keyboard="false">
		  	<div class="modal-dialog modal-lg" role="document">
				<div class="modal-content">
					<div class="modal-header">
						<h5 class="modal-title" id="RcvModalLabel">รายละเอียด</h5>
						<button type="button" class="close" data-dismiss="modal" aria-label="Close">
						<span aria-hidden="true">&times;</span>
						</button>
					</div>
					<div class="modal-body">
						<div id="ShowDetailSMPO"></div>
					</div>
					<div class="modal-footer">
						<button type="button" class="btn btn-secondary btn-sm" data-dismiss="modal"><i class="fas fa-times"></i> ปิด</button>
					</div>
				</div>
		  	</div>
		</div>