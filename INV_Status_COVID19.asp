<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%
session("DEPT_ID") =""
Dim Drug
Dim Drug_numRows

Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.ActiveConnection = MM_INVFlood_STRING

Function Urate(Number)
Dim A, B, C, D, E, F, G
		if number = 0 then
		urate = 1 
		else
		urate = number
		end if
End Function

if request.QueryString("search") <> "" then 
		keyword = request.QueryString("search")
elseif request.QueryString("SearchMenu") <> "" then 
		keyword = request.QueryString("SearchMenu")
end if
		
if keyword <> "" then 
	Drug.Source = "SELECT GROUP1.G1_NAME, INV_MD.VEN, INV_MD.WORKING_CODE, INV_MD.DRUG_NAME, INV_MD.SALE_UNIT, STD_RATIO3,QTY_ON_HAND/std_ratio3 AS RemainPack, " & _
				"(QTY_ON_HAND/RATE_PER_MONTH)*30 AS RemainDayForUse, MIN_LEVEL/std_ratio3 AS MIN_No, MAX_LEVEL/std_ratio3 AS MAX_No, INV_MD.RATE_PER_MONTH/30 as RatePerDay, " & _
				"(SELECT Sum(QTY_ORDER-ISNULL(QTY_ORDER_RCV,0)+isnull(QTY_FREE,0)-isnull(QTY_FREE_RCV,0)) AS QTY_DIFF " & _
				"FROM MS_PO_C INNER JOIN MS_PO ON MS_PO_C.PO_NO = MS_PO.PO_NO " & _
				"WHERE (((MS_PO.Status) = '1' Or (MS_PO.Status) Is Null Or (MS_PO.Status) = 'A' Or (MS_PO.Status) = 'B') And ((MS_PO_C.RCV_FLAG) Is Null) And WORKING_CODE=INV_MD.WORKING_CODE) " & _
				"GROUP BY MS_PO_C.WORKING_CODE) as Purchasing " & _
				"FROM INV_MD LEFT JOIN (TBLDRUG_GROUP LEFT JOIN GROUP1 ON TBLDRUG_GROUP.G1_CODE = GROUP1.G1_CODE) ON INV_MD.GROUP_CODE = TBLDRUG_GROUP.G_CODE " & _
				"WHERE (((INV_MD.RATE_PER_MONTH)<>0) AND ((INV_MD.NOUSE) Is Null Or (INV_MD.NOUSE)='') AND ((INV_MD.OUT_OF_LIST) Is Null Or (INV_MD.OUT_OF_LIST)='') AND ((INV_MD.ROP_EXCEPT) Is Null Or (INV_MD.ROP_EXCEPT)='') AND (ED_NED='1' or ED_NED='2') " & _
				"AND ((INV_MD.DRUG_NAME  Like '%" + Replace(keyword, "'", "''") + "%') or (GROUP1.G1_NAME Like '%" + Replace(keyword, "'", "''") + "%'))) " & _
				"order by G1_NAME, (QTY_ON_HAND/RATE_PER_MONTH)*30"




				'Drug.Source =  "SELECT INV_MD.* FROM INV_MD WHERE (INV_MD.NOUSE Is Null) AND (([drug_name]  Like '%" + Replace(keyword, "'", "''") + "%')"  & _
				'" or ([composition] Like '%" + Replace(keyword, "'", "''") + "%') or ([HOSP_CODE] Like '%" + Replace(keyword, "'", "''") + "%') or ([WORKING_CODE] Like '%" + Replace(keyword, "'", "''") + "%'))"'
else
	Drug.Source = "SELECT GROUP1.G1_NAME, INV_MD.VEN, INV_MD.WORKING_CODE, INV_MD.DRUG_NAME, INV_MD.SALE_UNIT, STD_RATIO3,QTY_ON_HAND/std_ratio3 AS RemainPack, " & _
				"(QTY_ON_HAND/RATE_PER_MONTH)*30 AS RemainDayForUse, MIN_LEVEL/std_ratio3 AS MIN_No, MAX_LEVEL/std_ratio3 AS MAX_No, INV_MD.RATE_PER_MONTH/30 as RatePerDay, " & _
				"(SELECT Sum(QTY_ORDER-ISNULL(QTY_ORDER_RCV,0)+isnull(QTY_FREE,0)-isnull(QTY_FREE_RCV,0)) AS QTY_DIFF " & _
				"FROM MS_PO_C INNER JOIN MS_PO ON MS_PO_C.PO_NO = MS_PO.PO_NO " & _
				"WHERE (((MS_PO.Status) = '1' Or (MS_PO.Status) Is Null Or (MS_PO.Status) = 'A' Or (MS_PO.Status) = 'B') And ((MS_PO_C.RCV_FLAG) Is Null) And WORKING_CODE=INV_MD.WORKING_CODE) " & _
				"GROUP BY MS_PO_C.WORKING_CODE) as Purchasing " & _
				"FROM INV_MD LEFT JOIN (TBLDRUG_GROUP LEFT JOIN GROUP1 ON TBLDRUG_GROUP.G1_CODE = GROUP1.G1_CODE) ON INV_MD.GROUP_CODE = TBLDRUG_GROUP.G_CODE " & _
				"WHERE (((INV_MD.RATE_PER_MONTH)<>0) AND ((INV_MD.NOUSE) Is Null Or (INV_MD.NOUSE)='') AND ((INV_MD.OUT_OF_LIST) Is Null Or (INV_MD.OUT_OF_LIST)='') AND ((INV_MD.ROP_EXCEPT) Is Null Or (INV_MD.ROP_EXCEPT)='') AND (ED_NED='1' or ED_NED='2') AND ((QTY_ON_HAND/RATE_PER_MONTH)*30)<=30) " & _
				"order by G1_NAME, (QTY_ON_HAND/RATE_PER_MONTH)*30"
end if 

Drug.CursorType = 3
Drug.CursorLocation = 3
Drug.LockType = 1
Drug.Open()

Drug_numRows = 0

Dim Repeat1__numRows
Dim Repeat1__index

Repeat1__numRows = -1
Repeat1__index = 0
Drug_numRows = Drug_numRows + Repeat1__numRows
%>


<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />

    <!-- jQuery (necessary for Bootstrap's JavaScript plugins) -->
    <!-- script src="http://code.jquery.com/jquery-latest.min.js"></script>
    <!-- Include all compiled plugins (below), or include individual files as needed -->
	

<title>ระบบรายงานปริมาณยาและเวชภัณฑ์คงคลัง</title>

    <!-- Bootstrap -->
    
    <link rel="stylesheet" href="css/bootstrap.min.css"> 
	<script src="js/jquery-3.2.1.min.js"></script>
	<script src="js/bootstrap.min.js"></script>
    <!-- HTML5 shim and Respond.js for IE8 support of HTML5 elements and media queries -->
    <!-- WARNING: Respond.js doesn't work if you view the page via file:// -->
    <!--[if lt IE 9]>
      <script src="https://oss.maxcdn.com/html5shiv/3.7.3/html5shiv.min.js"></script>
      <script src="https://oss.maxcdn.com/respond/1.4.2/respond.min.js"></script>
    <![endif]-->


<nav class="navbar navbar-default">
	<div class="container-fluid">
		<!-- Brand and toggle get grouped for better mobile display -->
		<div class="navbar-header">
		  <button type="button" class="navbar-toggle collapsed" data-toggle="collapse" data-target="#bs-example-navbar-collapse-1" aria-expanded="false">
			<span class="sr-only">Toggle navigation</span>
			<span class="icon-bar"></span>
			<span class="icon-bar"></span>
			<span class="icon-bar"></span>
		  </button>
		  <a class="navbar-brand" href="default.asp">INVC</a>
		</div>

		<!-- Collect the nav links, forms, and other content for toggling -->
		<div class="collapse navbar-collapse" id="bs-example-navbar-collapse-1">

		  <form class="navbar-form navbar-left" id="form2" name="form2" method="get" action="INV_Status_COVID19.asp">
			<div class="form-group">
					<a class="btn btn-default" role="button" href="default.asp">HOME</a>

			
			
			  <input name="SearchMenu"  type="text"  id="SearchMenu" class="form-control" size="20" placeholder="ชื่อยา กลุ่มยา">
			</div>
			<button type="submit" class="btn btn-primary">ค้นหา<button>
		  </form>
		</div>
	</div>
</nav>


  </head>
<body>
<p>
<div class="container" align="center">
		<div class="row">
        


    <h2 align="center" style="color :red"> รายงานปริมาณยาสำรองคลัง <30 วัน เพื่อรองรับสถานการณ์ฉุกเฉิน COVID-19 </h2> 
	
	


<% if keyword <>"" then
response.write("ผลการค้นหา ( ")
response.write(keyword) 
response.write(" ) พบ ")
response.write(drug.recordcount) 
response.write(" รายการ")
end if
%>       
                
                
<table class="w-100">
	<tr>
		<td valign="top" class="p-2 w-100">
			<div class="card">
				<div class="card-body">
					<div class="mb-2">
				
				
<table class="table table-bordered" align="center" >
                <thead>
  <tr>
    <th width="35%" ><div align="center">กลุ่มยา<div></th>
    <th width="35%" ><div align="center">ชื่อยา</div></th>
    <th width="5%" ><div align="center">VEN</div></th>
    <th width="5%" ><div align="center">คงเหลือ</div></th>
    <th width="5%" ><div align="center">หน่วย</div></th>
    <th width="5%" ><div align="center">พอใช้(วัน)</div></th>
    <th width="5%" ><div align="center">Rate/Day </div></th>
    <th width="5%" >ค้างส่ง</th>
    
  </tr>
  </thead>
  <tbody>
  <%  runno = 1
While ((Repeat1__numRows <> 0) AND (NOT Drug.EOF)) 
%>

    <tr> 
		<td ><div align="left">
		<%	
			
			if Drug("G1_NAME") = "" or isnull(Drug("G1_NAME")) = true then
				Grp1 = "N/A"
			else
				Grp1 = (Drug("G1_NAME"))
			end if 
			
			
			if Grp1 = Grp2 then 
			else 
				response.write (Grp1)
			end if
	
		%>
			</div></td>
		<td ><%=(Drug.Fields.Item("drug_name").Value)%></td>
		<td ><div align="center"><%=(Drug.Fields.Item("VEN").Value)%> </div></td>
	    <td ><div align="center"><%=(Drug.Fields.Item("RemainPack").Value)%></div></td>
	    <td ><div align="left"><%=(Drug.Fields.Item("SALE_UNIT").Value)%></div></td>
	    <td ><div align="right"><%=round(Drug.Fields.Item("RemainDayForUse").Value)%></div></td>
	    <td ><div align="right"> <%=round(Drug.Fields.Item("RatePerDay").Value,2)%></td>
	    <td ><div align="right"> <%=(Drug.Fields.Item("Purchasing").Value)%></td>
<tr>
   
<%
  Repeat1__index=Repeat1__index+1
  at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1
  Grp2= Grp1
  Drug.MoveNext()
Wend
%>
</tbody>
</table>
               
	  </div>
  </div>

  </body>
</html>

<%
Drug.Close()
Set Drug = Nothing
%>