<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->


<%
mode = request.querystring("mode")
keyword = request.querystring("keyword")

if keyword <> "" then 

	sql = "select WORKING_CODE,DRUG_NAME, QTY_ON_HAND, TOTAL_COST, TOTAL_VALUE, VEN, ABC,LOCATION, DOSAGE_FORM, STD_RATIO3, STD_PRICE3, REORDER_QTY,MIN_LEVEL, RATE_PER_MONTH, (select Sum(SUBSTOCK.QTY_ON_HAND) from SUBSTOCK WHERE SUBSTOCK.WORKING_CODE=INV_MD.WORKING_CODE)  as QTY_Substock from INV_MD where NOUSE is null and VEN in ('V','E') and QTY_ON_HAND >0 and DRUG_NAME+DRUG_NAME_KEY like '%" & keyword & "%' order by TOTAL_VALUE DESC "

else 

	if mode = "VE" then
		sql = "select WORKING_CODE,DRUG_NAME, QTY_ON_HAND, TOTAL_COST, TOTAL_VALUE, VEN, ABC,LOCATION, DOSAGE_FORM, STD_RATIO3, STD_PRICE3, REORDER_QTY,MIN_LEVEL, RATE_PER_MONTH, (select Sum(SUBSTOCK.QTY_ON_HAND) from SUBSTOCK WHERE SUBSTOCK.WORKING_CODE=INV_MD.WORKING_CODE)  as QTY_Substock from INV_MD where NOUSE is null and VEN in ('V','E') and QTY_ON_HAND >0 order by VEN DESC, TOTAL_VALUE DESC"
	elseif mode = "HighCost" then
		sql = "select WORKING_CODE,DRUG_NAME, QTY_ON_HAND, TOTAL_COST, TOTAL_VALUE, VEN, ABC,LOCATION, DOSAGE_FORM, STD_RATIO3, STD_PRICE3, REORDER_QTY,MIN_LEVEL, RATE_PER_MONTH, (select Sum(SUBSTOCK.QTY_ON_HAND) from SUBSTOCK WHERE SUBSTOCK.WORKING_CODE=INV_MD.WORKING_CODE) as QTY_Substock from INV_MD where NOUSE is null and QTY_ON_HAND >0 order by TOTAL_VALUE DESC "
	end if 

end if 

Set EOC = Server.CreateObject("ADODB.Recordset")
EOC.Open sql, Conn, 1,3





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

function FillData(){

	$("input[name=SelectMode]").val(["<%=mode%>"]);
	$("#keyword").val('<%=keyword%>');
}

</script>

<body class="fixed-nav sticky-footer bg-dark" id="page-top" onload="FillData()">

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
        <div class="card-header">รายการยาที่เฝ้าระวังในสถานการณ์น้ำท่วมเชียงราย ปี 2567</div>
		<div class="card-header">
			<table width="100%">
				<tr>
					<td align="left">
						<div>
							<i class="fa fa-table"></i> แสดง   | 
							<input type="radio" id="SelectMode1" name ="SelectMode" value="VE" onclick="SelectMode('VE')"> V + E เรียง V -> E, มูลค่ารวม
							<input type="radio" id="SelectMode2" name ="SelectMode" value="HighCost" onclick="SelectMode('HighCost')"> ทุกรายการ เรียงตามมูลค่า
						</div>
					</td>
					<td> input
					</td>
					<td align="right">
						<div>
							ค้นหารายการ <input type ="text" id="keyword" name="keyword" onKeyUP="if(event.keyCode==13) { SearchKeyword(this.value) };"> 
							<button class="btn btn-primary btn-sm" onclick="SearchKeyword('')"><i class="fa fa-search"></i></button>
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
					<th><div align="center">รหัสยา</div></th>
					<th><div align="center">รายการยา/เวชภัณฑ์</div></th>
					<th><div align="center">รูปแบบ</div></th>
					<th><div align="center">ขนาดบรรจุ</div></th>
					<th ><div align="center">คลังใหญ่</div></th>
					<th ><div align="center">คลังย่อย</div></th>
					<th><div align="center">มูลค่าซื้อ</div></th>
					<th><div align="center">มูลค่ารวม</div></th>
					<th><div align="center">ABC</div></th>
					<th><div align="center">VEN</div></th>
					<th><div align="center">จุดสั่งซื้อ</div></th>
					<th><div align="center">Min</div></th>
					<th><div align="center">Rate/Month</div></th>
					<th><div align="center">Location</div></th>
				</tr>
				</thead>
				<tbody>
				<%  runno = 1
				While NOT EOC.EOF
				%>
					
					<tr> 

						<td><div align="center"><%=runno%></div></td>
						<td><div align="center"><%=EOC("WORKING_CODE")%></div></td>
						<td><div align="left"><%=(EOC("DRUG_NAME"))%></div></td>
						<td><div align="center"><%=EOC("DOSAGE_FORM")%></div></td>
						<td><div align="right"><%=EOC("STD_RATIO3")%></div></td>
						<td><div align="right"><%=formatnumber(EOC("QTY_ON_HAND"),0)%></div></td>
						<td><div align="right"><%=EOC("QTY_Substock")%></div></td>
						<td><div align="right"><%=formatnumber(EOC("TOTAL_COST"),2)%></div></td>
						<td><div align="right"><%=formatnumber(EOC("TOTAL_VALUE"),2)%></div></td>
						<td><div align="center"><%=EOC("ABC")%></div></td>
						<td><div align="center"><%=EOC("VEN")%></div></td>
						<td><div align="center"><%=EOC("REORDER_QTY")%></div></td>
						<td><div align="center"><%=EOC("MIN_LEVEL")%></div></td>
						<td><div align="center"><%=EOC("RATE_PER_MONTH")%></div></td>
						<td><div align="center"><%=EOC("LOCATION")%></div></td>
						
				<tr>
				
				<%
				
				runno = runno +1
				EOC.MoveNext()
				Wend

EOC.close 
set EOC = nothing 

%>
				</tbody>
			</table>

          </div>
        </div>
        <div class="card-footer small text-muted">Your IP address : <%Response.Write(Request.ServerVariables("remote_addr"))%></div>
      </div>
    </div>