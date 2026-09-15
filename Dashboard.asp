<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%


'YYY = request.QueryString("YYY")
' YYY = "65"
' POStatus = request.QueryString("POstatus")

' Set Drug = Server.CreateObject("ADODB.Recordset")
' Drug.ActiveConnection = MM_INVFlood_STRING

' Drug.Source = "SELECT MS_PO.PO_NO, MS_PO.REAL_PO, MS_PO.PO_DATE , MS_PO.DOC_NO, COMPANY.COMPANY_NAME_PO, MS_PO.TOTAL_ITEM, TOTAL_COST, TblPOStatus.StatusName, BILLIN, BILLout, BILLEND, BILLOUTACC, BILLOUTFIN, BDG_TYPE.BDGNAME, TBLBUY.BUYNAME FROM TBLBUY INNER JOIN (TblPOStatus INNER JOIN (BDG_TYPE INNER JOIN (TBLED_NED INNER JOIN (COMPANY INNER JOIN MS_PO ON COMPANY.COMPANY_CODE = MS_PO.VENDOR_CODE) ON TBLED_NED.EDCODE = MS_PO.ED_NED) ON BDG_TYPE.BDGCODE = MS_PO.BUDGET_TYPE) ON TblPOStatus.StatusCode = MS_PO.STATUS) ON TBLBUY.BUYCODE = MS_PO.BUY_METHOD WHERE STATUS not in ('0','C') and Left(MS_PO.PO_NO,2)='" & YYY & "' ORDER BY MS_PO.REAL_PO;"

' Drug.CursorType = 3
' Drug.CursorLocation = 3
' Drug.LockType = 1
' Drug.Open()

Session("BudgetYear") = request.querystring("BudgetYear")

if request.querystring("BudgetYear") <> "" then 
	sql = "SELECT BUDGET.[year], sum(BUDGET.money)  as SumBUDGET FROM BUDGET WHERE [year]='" & request.querystring("BudgetYear") & "' GROUP by [year]"
else  
	sql = "SELECT BUDGET.[year], sum(BUDGET.money)  as SumBUDGET FROM BUDGET WHERE BUDGET.BudgetOpen='O' GROUP BY [year]"
end if 
Set Budget = Server.CreateObject("ADODB.Recordset")
Budget.Open sql, Conn, 1,3

if Session("BudgetYear") <> "" then 
	Var_FiscalYear = Session("BudgetYear")
else 
	Var_FiscalYear = Budget("Year")
end if 

'response.write(Var_FiscalYear)

sql = "SELECT BUDGET.[year] FROM BUDGET GROUP BY [year] order by [year] desc"
Set BudgetSrc = Server.CreateObject("ADODB.Recordset")
BudgetSrc.Open sql, Conn, 1,3

sql = "SELECT  Count(MS_PO.PO_NO) AS ITEM, sum([TOTAL_COST]) as Sum_Value FROM MS_PO WHERE Left([PO_NO],2)='" & right(Var_FiscalYear,2) & "' and MS_PO.STATUS not in ('0','C')"
Set POValue = Server.CreateObject("ADODB.Recordset")
POValue.Open sql, Conn, 1,3

sql = "SELECT  Count(MS_PO.PO_NO) AS ITEM, sum([TOTAL_COST]) as Sum_Value FROM MS_PO WHERE Left([PO_NO],2)='" & right(Var_FiscalYear,2) & "' and MS_PO.STATUS in ('2','3','4','5','6','7','8','9','D')"
Set RCVValue = Server.CreateObject("ADODB.Recordset")
RCVValue.Open sql, Conn, 1,3

sql = "SELECT  Count(MS_PO.PO_NO) AS ITEM, sum([TOTAL_COST]) as Sum_Value FROM MS_PO WHERE Left([PO_NO],2)='" & right(Var_FiscalYear,2) & "' and MS_PO.STATUS in ('4','5','6','7','8','9','D')"
Set ACCValue = Server.CreateObject("ADODB.Recordset")
ACCValue.Open sql, Conn, 1,3

sql = "SELECT  Count(MS_PO.PO_NO) AS ITEM, sum([TOTAL_COST]) as Sum_Value FROM MS_PO WHERE Left([PO_NO],2)='" & right(Var_FiscalYear,2) & "' and MS_PO.STATUS in ('4','5','7','8','9')"
Set FINValue = Server.CreateObject("ADODB.Recordset")
FINValue.Open sql, Conn, 1,3

sql = "SELECT  Count(MS_PO.PO_NO) AS ITEM, sum([TOTAL_COST]) as Sum_Value FROM MS_PO WHERE Left([PO_NO],2)='" & right(Var_FiscalYear,2) & "' and MS_PO.STATUS in ('5','9')"
Set ENDValue = Server.CreateObject("ADODB.Recordset")
ENDValue.Open sql, Conn, 1,3

sql = "SELECT Sum(INV_MD.TOTAL_VALUE) AS SumOfTOTAL_VALUE FROM INV_MD"
Set MainINV = Server.CreateObject("ADODB.Recordset")
MainINV.Open sql, Conn, 1,3

sql = "SELECT Sum(Substock.TOTAL_VALUE) AS SumOfTOTAL_VALUE FROM Substock"
Set SubINV = Server.CreateObject("ADODB.Recordset")
SubINV.Open sql, Conn, 1,3

sql = "SELECT TOP 1 MBS_RE_M.YEAR, MBS_RE_M.MONTH, (SELECT SUM(MNTH_SUM.TOTAL_VALUE) FROM MNTH_SUM WHERE MNTH_SUM.YEAR = MBS_RE_M.YEAR AND MNTH_SUM.MONTH = MBS_RE_M.MONTH)/Sum(MBS_RE_M.SALE_VALUE) as REMAIN_RATIO FROM MBS_RE_M GROUP BY MBS_RE_M.YEAR, MBS_RE_M.MONTH ORDER BY MBS_RE_M.YEAR DESC , MBS_RE_M.MONTH DESC;"
Set RemainRatio = Server.CreateObject("ADODB.Recordset")
RemainRatio.Open sql, Conn, 1,3

sql = "SELECT sum((AgreeQty/PACK_RATIO)*UnitPrice) - sum((BuyQty/PACK_RATIO)*UnitPrice) as RemainAgreement, ((sum((AgreeQty/PACK_RATIO)*UnitPrice) - sum((BuyQty/PACK_RATIO)*UnitPrice))*100)/sum((AgreeQty/PACK_RATIO)*UnitPrice) as perbuy FROM Agreement INNER JOIN COMPANY ON Agreement.Company=COMPANY.COMPANY_CODE INNER JOIN TBLBUY ON Agreement.BuyMethod=TBLBUY.BUYCODE INNER JOIN INV_MD ON Agreement.WORKING_CODE=INV_MD.WORKING_CODE where agreement.BuyQty < agreement.AgreeQty and Expdate>getdate() "
Set RemainAgreement = Server.CreateObject("ADODB.Recordset")
RemainAgreement.Open sql, Conn, 1,3

sql = "SELECT TOP 1 LEFT(CONVERT(varchar, PO_date,112),6) as POMonth, avg(datediff(d,[PO_DATE],[BILLIN])) as SendDay, avg(datediff(d,[BILLIN],[BillOUT])) as DocDay, avg(datediff(d,[BILLOUT],[BillEND])) as AccDay from MS_PO  group by LEFT(CONVERT(varchar, PO_date,112),6) HAVING avg(datediff(d,[BILLOUT],[BillEND])) is not null order by LEFT(CONVERT(varchar, PO_date,112),6)  desc"
Set POProcess = Server.CreateObject("ADODB.Recordset")
POProcess.Open sql, Conn, 1,3


sql = "SELECT count(WORKING_CODE) as ItemNo from INV_MD where NoUse is null"
Set ItemNO = Server.CreateObject("ADODB.Recordset")
ItemNO.Open sql, Conn, 1,3

WORKING_CODE = request.querystring("WORKING_CODE")
WORKING_CODE = "2010930"
if WORKING_CODE <> "" then 
	sql = "select m.WORKING_CODE, DRUG_NAME, DOSAGE_FORM,STD_RATIO3,QTY_ON_HAND/std_ratio3 as REMAIN, sum(ACTIVE_QTY1+ACTIVE_QTY2+ACTIVE_QTY3) As SALE_QUAN, sum(c.[VALUE]) as SALE_VALUE, ABC,VEN, left(convert(CHAR(8),OPERATE_DATE,112),6) + 54300 as DMonth from CARD c INNER JOIN INV_MD m ON c.WORKING_CODE=m.WORKING_CODE AND R_S_STATUS ='S' WHERE c.WORKING_CODE ='" & WORKING_CODE & "' group by m.WORKING_CODE, DRUG_NAME, DOSAGE_FORM,STD_RATIO3,QTY_ON_HAND/std_ratio3,ABC,VEN, left(convert(CHAR(8),OPERATE_DATE,112),6) + 54300 order by left(convert(CHAR(8),OPERATE_DATE,112),6) + 54300 desc"

	Set ChartItem = Server.CreateObject("ADODB.Recordset")
	ChartItem.Open sql, Conn, 1,3
end if 


%>
<html>

<head>
  <meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
  <meta http-equiv="X-UA-Compatible" content="IE=edge">
  <meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no">
  <meta name="description" content="">
  <meta name="author" content="">
  <title>INVC </title>
  

  <!-- Bootstrap core CSS-->
  <link href="vendor/bootstrap/css/bootstrap.min.css" rel="stylesheet">
  <!-- Custom fonts for this template-->
  <link href="vendor/font-awesome/css/font-awesome.min.css" rel="stylesheet" type="text/css">
  <!-- Page level plugin CSS-->
  <link href="vendor/datatables/dataTables.bootstrap4.css" rel="stylesheet">
  <!-- Custom styles for this template-->
  <!-- <link href="css/sb-admin.css" rel="stylesheet">-->
	
  <link href="font-awesome-5.12.1/css/all.css" rel="stylesheet" type="text/css">
  <script src="js/jquery-3.2.1.min.js"></script>
  <script src="js/jquery.canvasjs.min.js"></script>
  <script src="bootstrap/js/bootstrap.min.js"></script>
  
</head>

<script>

function SubmitBudget(FiscalYear){
	//console.log('F' + FiscalYear);
	var budgetSrc = document.getElementById("budgetSrc").value;
	console.log(budgetSrc);
	//$.get( "SetSession.asp?BudgetYear=" + budgetSrc ).done(function( data ) {
		//location.reload();
		//console.log(<%=Var_FiscalYear%>);
		window.open("Dashboard.asp?BudgetYear=" + budgetSrc, "_self");
		//$("#budgetSrc").val(<%=Var_FiscalYear%>);
	//});
	
}

function ShowDataTable(FiscalYear,flag){
	
	$.get( "table_ipiss.asp?flag=" + flag + "&FiscalYear=" + FiscalYear  ).done(function( data ) {
			//console.log(data);
		$("#datatable").html(data);
		
	});
	
	if (flag == 'PO') {
		$("#header_name").html(' ใบสั่งซื้อทั้งหมด');
	} else if (flag == 'RCV') {
		$("#header_name").html(' ใบสั่งซื้อที่รับของแล้ว'); 
	} else if (flag == 'ACC') {
		$("#header_name").html(' ใบสั่งซื้อที่ส่งบัญชีแล้ว');
	} else if (flag == 'FIN') {
		$("#header_name").html(' ใบสั่งซื้อที่ส่งเอกสารตั้งเบิกให้การเงินแล้ว') 
	} else if (flag == 'END') {
		$("#header_name").html(' ใบสั่งซื้อที่ปิดบัญชีแล้ว') 
	}
}

function ShowItem(FiscalYear,flag){

	if (flag == 'item') {
		$("#header_name").html(' การจัดซื้อยาและเวชภัณฑ์ตามแผนจัดซื้อ');
		$.get( "table_ipiss_item.asp?FiscalYear=" + FiscalYear  ).done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		
		});
	} else if (flag == 'budget') {
		$("#header_name").html(' รายละเอียดงบประมาณประจำปี ' + FiscalYear );
		$.get( "ipiss_budget.asp?FiscalYear=" + FiscalYear  ).done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		
		});
	} else if (flag == 'MainINV') {
		$("#header_name").html(' มูลค่ายาและเวชภัณฑ์คงคลัง (คลังใหญ่)' );
		$.get( "ipiss_maininv_remain.asp").done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		
		});
	} else if (flag == 'Substock') {
		$("#header_name").html(' มูลค่ายาและเวชภัณฑ์คงคลัง (คลังย่อย)' );
		$.get( "ipiss_substock_remain.asp").done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		
		});
	} else if (flag == 'Agree') {
		$("#header_name").html(' รายละเอียดการทำสัญญา' );
		$.get( "ipiss_agree.asp").done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		
		});
	} else if (flag == 'Process') {
		$("#header_name").html(' สถานะใบสั่งซื้อและระยะเวลาดำเนินการในแต่ละขั้นตอน' );
		$.get( "ipiss_Process.asp?FiscalYear=" + FiscalYear).done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		
		});
	} else if (flag == 'Ratio') {
		$("#header_name").html(' ตัวชี้วัดการสำรองคลัง' );
		$.get( "ipiss_item_ratio.asp?FiscalYear=" + FiscalYear).done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		
		});
	} else if (flag == 'TopUsed') {
		$("#header_name").html(' มูลค่าการใช้ยาปี ' + FiscalYear + ' |   <input type="checkbox" id="ShowBuyUsed" value="buy" onclick="ShowBuyOnlyUsed()"> เฉพาะที่จัดซื้อ ');
		$.get( "table_disp.asp?FiscalYear=" + FiscalYear).done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		
		});
	} else if (flag == 'TopIncrease') {
		$("#header_name").html(' มูลค่าการใช้ยาที่เพิ่มขึ้นในปี ' + FiscalYear + ' เทียบกับปี ' + (FiscalYear-1) + ' |   <input type="checkbox" id="ShowBuy" value="buy" onclick="ShowBuyOnly()"> เฉพาะที่จัดซื้อ ');
		$.get( "table_disp_diff.asp?FiscalYear=" + FiscalYear).done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		
		});
	} else if (flag == 'WORKING_CODE') {
		$("#header_name").html(' <button class="btn btn-success btn-sm" onclick="ShowItemSearchModal()"><i class="fas fa-search"></i> ค้นหา</button>' + ' ข้อมูลรายการยาและเวชภัณฑ์');
		$.get( "table_by_item.asp?FiscalYear=" + FiscalYear).done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		
		});
	} else if (flag == 'RCV_DISP_Mnth') {
		$("#header_name").html(' ข้อมูลรับ/จ่ายยาและเวชภัณฑ์รายเดือน'  + FiscalYear + ' |   <input type="checkbox" id="ShowRcvDisp" value="buy" onclick="ShowRcvDisp()"> เฉพาะที่จัดซื้อ ');
		$.get( "table_monthly_rpt.asp?FiscalYear=" + FiscalYear).done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		
		});
	}
}

function ShowRcvDisp(){
	
	var FiscalYear = <%=Var_FiscalYear%>
	
	if (document.getElementById('ShowRcvDisp').checked){
		//$("#header_name").html(' มูลค่าการใช้ยาที่เพิ่มขึ้นในปี ' + FiscalYear + ' เทียบกับปี ' + (FiscalYear-1) + ' |   <input type="checkbox" id="ShowBuy" value="buy" onclick="ShowBuyOnly()" checked> เฉพาะที่จัดซื้อ ');
		//$("#SMP").prop('checked',true);
		$.get( "table_monthly_rpt.asp?FiscalYear=" + FiscalYear + "&mode=buy").done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		});
	} else {
		
		//$("#header_name").html(' มูลค่าการใช้ยาที่เพิ่มขึ้นในปี ' + FiscalYear + ' เทียบกับปี ' + (FiscalYear-1) + ' |   <input type="checkbox" id="ShowBuy" value="buy" onclick="ShowBuyOnly()"> เฉพาะที่จัดซื้อ ');
		$.get( "table_monthly_rpt.asp?FiscalYear=" + FiscalYear).done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		});
	}
}

function ShowBuyOnly(){
	
	var FiscalYear = <%=Var_FiscalYear%>
	
	if (document.getElementById('ShowBuy').checked){
		$("#header_name").html(' มูลค่าการใช้ยาที่เพิ่มขึ้นในปี ' + FiscalYear + ' เทียบกับปี ' + (FiscalYear-1) + ' |   <input type="checkbox" id="ShowBuy" value="buy" onclick="ShowBuyOnly()" checked> เฉพาะที่จัดซื้อ ');
		$.get( "table_disp_diff.asp?FiscalYear=" + FiscalYear + "&mode=buy").done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		});
	} else {
		
		$("#header_name").html(' มูลค่าการใช้ยาที่เพิ่มขึ้นในปี ' + FiscalYear + ' เทียบกับปี ' + (FiscalYear-1) + ' |   <input type="checkbox" id="ShowBuy" value="buy" onclick="ShowBuyOnly()"> เฉพาะที่จัดซื้อ ');
		$.get( "table_disp_diff.asp?FiscalYear=" + FiscalYear).done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		});
	}
}

function ShowBuyOnlyUsed(){
	
	var FiscalYear = <%=Var_FiscalYear%>
	
	if (document.getElementById('ShowBuyUsed').checked){
		//$("#header_name").html(' มูลค่าการใช้ยาที่เพิ่มขึ้นในปี ' + FiscalYear + ' เทียบกับปี ' + (FiscalYear-1) + ' |   <input type="checkbox" id="ShowBuy" value="buy" onclick="ShowBuyOnly()" checked> เฉพาะที่จัดซื้อ ');
		//$("#SMP").prop('checked',true);
		$.get( "table_disp.asp?FiscalYear=" + FiscalYear + "&mode=buy").done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		});
	} else {
		
		//$("#header_name").html(' มูลค่าการใช้ยาที่เพิ่มขึ้นในปี ' + FiscalYear + ' เทียบกับปี ' + (FiscalYear-1) + ' |   <input type="checkbox" id="ShowBuy" value="buy" onclick="ShowBuyOnly()"> เฉพาะที่จัดซื้อ ');
		$.get( "table_disp.asp?FiscalYear=" + FiscalYear).done(function( data ) {
			//console.log(data);
			$("#datatable").html(data);
		});
	}
}


function ShowPODetail(PO_NO){
	console.log(PO_NO);
	$("#PODetailModal").modal('show');
	
	$("#exampleModalLabel").html('ข้อมูลใบสั่งซื้อเลขที่ ' + PO_NO);
	
	$.get( "PODetail.asp?showMenu=Off&RPO=" + PO_NO  ).done(function( data ) {
			console.log(data);
		$("#PODetailBody").html(data);
	});
	
}

function SearchPO(FiscalYear){

	$("#exampleModalLabel").html('ค้นหาใบสั่งซื้อ');
	
	$.get( "PO_Search.asp?showMenu=Off&FiscalYear="+FiscalYear).done(function( data ) {
			//console.log(data);
		$("#datatable").html(data);
		
	});
	$("#header_name").html(' ค้นหาใบสั่งซื้อ');
}

function ShowItemSearchModal(){
	$("#SearchItemModal").modal('show')
}

function searchItem(SearchKey){
	//alert(PaType);
	//alert( $('#AN').val() );
	if( SearchKey != '' ){
		//$('.Loading').show();
		$.get( "searchItem.asp?SearchKey="+SearchKey ).done(function( data ) {
			//console.log(data);
			$("#ItemSearchDiv").html(data);
		});
	}
}

function selectItemSearch(WORKING_CODE,DRUG_NAME){
	
	$("#header_name").html(' <button class="btn btn-success btn-sm" onclick="ShowItemSearchModal()"><i class="fas fa-search"></i> ค้นหา</button>' + ' ข้อมูลรายการยาและเวชภัณฑ์ ' + WORKING_CODE + ' ' + DRUG_NAME);
	$("#SearchItemModal").modal('hide');
	$.get( "table_by_item.asp?WORKING_CODE=" + WORKING_CODE).done(function( data ) {
		$("#datatable").html(data);	
		callChart();
	});
	
}

function PO_detail_in_dashboard(){

	var RPO = document.getElementById("RPO").value;
	$.get( "PODetail.asp?showMenu=Off&RPO=" + RPO  ).done(function( data ) {
			//console.log(data);
		$("#datatable").html(data);
	});

}

function PO_detail_in_dashboard_BillOutFin() {
	var SelectedDate = document.getElementById("SelectedDate").value;
	$("#header_name").html(' ใบสั่งซื้อที่ส่งให้การเงินวันที่ ' + SelectedDate );
	$.get( "table_ipiss.asp?SelectedDate=" + SelectedDate  ).done(function( data ) {
			//console.log(data);
		$("#datatable").html(data);
		
	});

}


function FillBudget(){
	$("#budgetSrc").val(<%=Var_FiscalYear%>);
}


var options = {
	animationEnabled: true,
	theme: "light2",
	title:{
		text: "สรุปจำนวนและมูลค่าการใช้",
		fontColor: "red",
		fontSize: 22
	},
	axisX:{
		valueFormatString: "####"
	},
	axisY: {
		title: "จำนวน/มูลค่า",
		suffix: "",
		minimum: 0
	},
	toolTip:{
		shared:true
	},  
	legend:{
		cursor:"pointer",
		verticalAlign: "top",
		horizontalAlign: "right",
		dockInsidePlotArea: true,
		itemclick: toogleDataSeries
	},
	data: [{
		type: "line",
		showInLegend: true,
		name: "จำนวน",
		markerType: "square",
		xValueFormatString: "####",
		color: "#F08080",
		yValueFormatString: "#,###",
		dataPoints: [

		{ x: 256512, y: 150 },

		
		
			{ x: 256511, y: 375 },

		
		
			{ x: 256510, y: 195 }
	
		]
	}
	]
};

function callChart(){

	$("#chartContainer").CanvasJSChart(options);
}

function toogleDataSeries(e){
	if (typeof(e.dataSeries.visible) === "undefined" || e.dataSeries.visible) {
		e.dataSeries.visible = false;
	} else{
		e.dataSeries.visible = true;
	}	e.chart.render();
}



</script>

<body class="fixed-nav sticky-footer bg-dark" id="page-top" onload="FillBudget()">
  <div class="content-wrapper">
    <div class="container-fluid"> 
		<br>
      <!-- Breadcrumbs-->
      <ol class="breadcrumb">
        <li class="breadcrumb-item"> INVC Dashboard : : เมนู
          <a href="#"></a>
        </li>
		<li class="breadcrumb-item"> 
			<a href="#" onclick="SearchPO('<%=Var_FiscalYear%>')">ค้นหาใบสั่งซื้อ</a>
		</li>
		<li class="breadcrumb-item"> 
			<a href="#" onclick="ShowItem('<%=Var_FiscalYear%>','TopUsed')">Top Used</a>
		</li>
		<li class="breadcrumb-item"> 
			<a href="#" onclick="ShowItem('<%=Var_FiscalYear%>','TopIncrease')">Top Increase</a>
		</li>
		<li class="breadcrumb-item"> 
			<a href="#" onclick="ShowItem('<%=Var_FiscalYear%>','WORKING_CODE')">ระบุรายการ</a>
		</li>
		<li class="breadcrumb-item"> 
			<a href="#" onclick="ShowItem('<%=Var_FiscalYear%>','RCV_DISP_Mnth')">รับ-จ่ายรายเดือน</a>
		</li>
      </ol>
      <!-- Icon Cards-->
      <div class="row">
        <div class="col-xl-2 col-sm-6 mb-3">
          <div class="card text-white bg-primary o-hidden h-100">
            <div class="card-body">
              <div class="mr-5">งบจัดซื้อปี  
				<select id="budgetSrc" name="budgetSrc" onchange="SubmitBudget(this.value)">
					<%while not BudgetSrc.EOF%>
					<option value="<%=BudgetSrc("year")%>"><%=BudgetSrc("year")%></option>
					<%BudgetSrc.movenext
					wend%>
				</select>
			   <br> <%=formatnumber(Budget("SumBUDGET"))%></div>
            </div>
            <a class="card-footer text-white clearfix small z-1" href="#" onclick="ShowItem('<%=Var_FiscalYear%>','budget')">
              <span class="float-left">ดูรายละเอียด</span>
              <span class="float-right">
                <i class="fa fa-angle-right"></i>
              </span>
            </a>
          </div>
        </div>
        <div class="col-xl-2 col-sm-6 mb-3">
          <div class="card text-white bg-success o-hidden h-100">
            <div class="card-body">
              <div class="mr-5">สั่งซื้อแล้ว  <br><%=formatnumber(POValue("Sum_Value"))%> บาท  </div>
            </div>
            <a class="card-footer text-white clearfix small z-1" href="#" onclick="ShowDataTable('<%=Var_FiscalYear%>','PO')">
              <span class="float-left">ดูรายละเอียด (<%=POValue("ITEM")%> ใบสั่งซื้อ)</span>
              <span class="float-right">
                <i class="fa fa-angle-right"></i>
              </span>
            </a>
          </div>
        </div>
        <div class="col-xl-2 col-sm-6 mb-3">
          <div class="card text-white bg-warning o-hidden h-100">
            <div class="card-body">
              <div class="mr-5">ตรวจรับแล้ว <br><%=formatnumber(RCVValue("Sum_Value"))%> บาท </div>
            </div>
            <a class="card-footer text-white clearfix small z-1" href="#" onclick="ShowDataTable('<%=Var_FiscalYear%>','RCV')">
              <span class="float-left">ดูรายละเอียด (<%=RCVValue("ITEM")%> ใบ)</span>
              <span class="float-right">
                <i class="fa fa-angle-right"></i>
              </span>
            </a>
          </div>
        </div>
        <div class="col-xl-2 col-sm-6 mb-3">
          <div class="card text-white bg-danger o-hidden h-100">
            <div class="card-body">
              <div class="mr-5">ส่งบัญชีรับรู้หนี้ <br><%=formatnumber(ACCValue("Sum_Value"))%> บาท </div>
            </div>
            <a class="card-footer text-white clearfix small z-1" href="#" onclick="ShowDataTable('<%=Var_FiscalYear%>','ACC')">
              <span class="float-left">ดูรายละเอียด (<%=ACCValue("ITEM")%> ใบ)</span>
              <span class="float-right">
                <i class="fa fa-angle-right"></i>
              </span>
            </a>
          </div>
        </div>
		<div class="col-xl-2 col-sm-6 mb-3">
          <div class="card text-white bg-secondary o-hidden h-100">
            <div class="card-body">
              <div class="mr-5">ส่งเอกสารตั้งเบิกการเงิน <br><%=formatnumber(FINValue("Sum_Value"))%> บาท </div>
            </div>
            <a class="card-footer text-white clearfix small z-1" href="#" onclick="ShowDataTable('<%=Var_FiscalYear%>','FIN')">
              <span class="float-left">ดูรายละเอียด (<%=FINValue("ITEM")%> ใบ)</span>
              <span class="float-right">
                <i class="fa fa-angle-right"></i>
              </span>
            </a>
          </div>
        </div>
		<div class="col-xl-2 col-sm-6 mb-3">
          <div class="card text-white bg-info o-hidden h-100">
            <div class="card-body">
              <div class="mr-5">ปิดบัญชีแล้ว <br><%=formatnumber(ENDValue("Sum_Value"))%> บาท </div>
            </div>
            <a class="card-footer text-white clearfix small z-1" href="#" onclick="ShowDataTable('<%=Var_FiscalYear%>','END')">
              <span class="float-left">ดูรายละเอียด (<%=ENDValue("ITEM")%> ใบ)</span>
              <span class="float-right">
                <i class="fa fa-angle-right"></i>
              </span>
            </a>
          </div>
        </div>
      </div>

	<div class="row">
        <div class="col-xl-2 col-sm-6 mb-3">
          <div class="card text-white bg-primary o-hidden h-100">
            <a class="card-footer text-white clearfix small z-1" href="#" onclick="ShowItem('<%=Var_FiscalYear%>','MainINV')">
              <span class="float-left">มูลค่าคลังใหญ่ <%=formatnumber(MainINV("SumOfTOTAL_VALUE"))%></span>
              <span class="float-right">
                <i class="fa fa-angle-right"></i>
              </span>
            </a>
          </div>
        </div>	
		<div class="col-xl-2 col-sm-6 mb-3">
          <div class="card text-white bg-success o-hidden h-100">
            <a class="card-footer text-white clearfix small z-1" href="#" onclick="ShowItem('<%=Var_FiscalYear%>','Substock')">
              <span class="float-left">มูลค่าคลังย่อย <%=formatnumber(SubINV("SumOfTOTAL_VALUE"))%></span>
              <span class="float-right">
                <i class="fa fa-angle-right"></i>
              </span>
            </a>
          </div>
        </div>	
		<div class="col-xl-2 col-sm-6 mb-3">
          <div class="card text-white bg-warning o-hidden h-100">
            <a class="card-footer text-white clearfix small z-1" href="#" onclick="ShowItem('<%=Var_FiscalYear%>','Ratio')">
              <span class="float-left">ปริมาณสำรองคลัง <%=round(RemainRatio("REMAIN_RATIO"),2)%> เดือน</span>
              <span class="float-right">
                <i class="fa fa-angle-right"></i>
              </span>
            </a>
          </div>
        </div>	
		<div class="col-xl-2 col-sm-6 mb-3">
          <div class="card text-white bg-danger o-hidden h-100">
            <a class="card-footer text-white clearfix small z-1" href="#" onclick="ShowItem('<%=Var_FiscalYear%>','Agree')">
              <span class="float-left">มูลค่าสัญญาคงค้าง 
				<%
				if not RemainAgreement.eof then 
					if isnull(RemainAgreement("RemainAgreement")) = false then 
						response.write formatnumber(RemainAgreement("RemainAgreement")) & " (" & round(RemainAgreement("perbuy"),2) & "%)"
					else 
						response.write "0.00"
					end if 
				end if 
				%>
			  </span>
              <span class="float-right">
                <i class="fa fa-angle-right"></i>
              </span>
            </a>
          </div>
        </div>	
		<div class="col-xl-2 col-sm-6 mb-3">
          <div class="card text-white bg-secondary o-hidden h-100">
            <a class="card-footer text-white clearfix small z-1" href="#" onclick="ShowItem('<%=Var_FiscalYear%>','Process')">
              <span class="float-left">ส่งของ <%=POProcess("SendDay")%>, ส่งบัญชี <%=POProcess("DocDay")%> , ส่งการเงิน <%=POProcess("AccDay")%> </span>
              <span class="float-right">
                <i class="fa fa-angle-right"></i>
              </span>
            </a>
          </div>
        </div>	
		<div class="col-xl-2 col-sm-6 mb-3">
          <div class="card text-white bg-info o-hidden h-100">
            <a class="card-footer text-white clearfix small z-1" href="#" onclick="ShowItem('<%=Var_FiscalYear%>','item')">
              <span class="float-left">แสดงรายการ (<%=ItemNO("ItemNo")%> รายการ)</span>
              <span class="float-right">
                <i class="fa fa-angle-right"></i>
              </span>
            </a>
          </div>
        </div>	
    </div> 
	   
	   
	  <!--div id="chartContainer2" style="height: 300px; width: 100%;"></div--> 
<script>
	//callChart();
</script>
      <!-- Example DataTables Card-->
      <div class="card mb-3">
        <div class="card-header">
          <i class="fa fa-table" id="header_name"></i>
			<%%>
		  </div>
        <div class="card-body">
          <div class="table-responsive">
            <div id="datatable"></div>
          </div>
        </div>
        <div class="card-footer small text-muted">Updated yesterday at 11:59 PM</div>
      </div>
    </div>
    <!-- /.container-fluid-->
    <!-- /.content-wrapper-->
    <footer class="sticky-footer">
      <div class="container">
        <div class="text-center">
          <small>Copyright &copy; Your Website 2018</small>
        </div>
      </div>
    </footer>
    <!-- Scroll to Top Button-->
    <a class="scroll-to-top rounded" href="#page-top">
      <i class="fa fa-angle-up"></i>
    </a>
	
	
<!-- PO detail Modal-->
    <div class="modal fade" id="PODetailModal" tabindex="-1" role="dialog" aria-labelledby="PODetailModal" aria-hidden="true">
      <div class="modal-dialog" style="max-width: 80%;" role="document">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title" id="exampleModalLabel"></h5>
            <button class="close" type="button" data-dismiss="modal" aria-label="Close">
              <span aria-hidden="true">x</span>
            </button>
          </div>
          <div class="modal-body" id="PODetailBody"></div>
          <div class="modal-footer">
            <button class="btn btn-secondary" type="button" data-dismiss="modal">Close</button>
          </div>
        </div>
      </div>
    </div>

   
    <!-- Bootstrap core JavaScript-->
    <script src="vendor/jquery/jquery.min.js"></script>
    <script src="vendor/bootstrap/js/bootstrap.bundle.min.js"></script>
    <!-- Core plugin JavaScript-->
    <script src="vendor/jquery-easing/jquery.easing.min.js"></script>
    <!-- Page level plugin JavaScript-->
    <script src="vendor/chart.js/Chart.min.js"></script>
    <script src="vendor/datatables/jquery.dataTables.js"></script>
    <script src="vendor/datatables/dataTables.bootstrap4.js"></script>
    <!-- Custom scripts for all pages-->
    <!-- <script src="js/sb-admin.min.js"></script>-->
    <!-- Custom scripts for this page-->
    <!-- <script src="js/sb-admin-datatables.min.js"></script>-->
    <!-- <script src="js/sb-admin-charts.min.js"></script>-->
  </div>
</body>

</html>
