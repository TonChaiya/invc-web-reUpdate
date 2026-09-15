<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%



Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.ActiveConnection = MM_INVFlood_STRING

search = request.QueryString("search")
Drug.Source = "SELECT *  FROM DRUG_VN WHERE ([BAR_CODE] Like '%" + Replace(search, "'", "''") + "%')"'
Drug.CursorType = 3
Drug.CursorLocation = 3
Drug.LockType = 1
Drug.Open()

Drug_numRows = 0


Set INVMD = Server.CreateObject("ADODB.Recordset")
INVMD.ActiveConnection = MM_INVFlood_STRING

INVMD.Source = "SELECT *  FROM INV_MD WHERE WORKING_CODE='" & drug("working_code") & "'"
INVMD.CursorType = 3
INVMD.CursorLocation = 3
INVMD.LockType = 1
INVMD.Open()

INVMD_numRows = 0


Set SMPO = Server.CreateObject("ADODB.Recordset")
SMPO.ActiveConnection = MM_INVFlood_STRING

SMPO.Source = "SELECT dbo.SM_PO.SUB_PO_NO, dbo.SM_PO.SUB_PO_DATE, dbo.DEPT_ID.DEPT_NAME, dbo.SM_PO_C.*, dbo.INV_MD.DRUG_NAME, dbo.INV_MD.SALE_UNIT, dbo.INV_MD.WORKING_CODE FROM INV_MD " &_
			  "INNER JOIN SM_PO_C ON INV_MD.WORKING_CODE = SM_PO_C.WORKING_CODE " &_
			  "INNER JOIN SM_PO ON SM_PO_C.SUB_PO_NO = SM_PO.SUB_PO_NO INNER JOIN DEPT_ID ON DEPT_ID.DEPT_ID = SM_PO.DEPT_ID WHERE SM_PO.SUB_PO_NO ='" & Session("SUB_PO_NO") & "' and SM_PO_C.WORKING_CODE='" & Drug("WORKING_CODE") & "'" 

SMPO.CursorType = 3
SMPO.CursorLocation = 3
SMPO.LockType = 1
SMPO.Open()

SMPO_numRows = 0


Set INVMDC= Server.CreateObject("ADODB.Recordset")
INVMDC.ActiveConnection = MM_INVFlood_STRING

INVMDC.Source = "SELECT *  FROM INV_MD_C WHERE WORKING_CODE='" & drug("working_code") & "' order by EXPIRED_DATE"

INVMDC.CursorType = 3
INVMDC.CursorLocation = 3
INVMDC.LockType = 1
INVMDC.Open()

INVMDC_numRows = 0


Set INVUser= Server.CreateObject("ADODB.Recordset")
INVUser.ActiveConnection = MM_INVFlood_STRING

INVUser.Source = "SELECT UserID, [name] FROM [USER] WHERE UserID=" & session("UserID")

INVUser.CursorType = 3
INVUser.CursorLocation = 3
INVUser.LockType = 1
INVUser.Open()

INVUser_numRows = 0

%>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<title>ปริมาณยาและเวชภัณฑ์คงคลัง</title>
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
    <!--#include file="menu.asp" -->
  </head>

<script>
function SaveChecked() {
	
	var SUB_PO_NO = '<%=SMPO("SUB_PO_NO")%>';
	var WORKING_CODE = '<%=SMPO("WORKING_CODE")%>';
		

	
	$.get( "SaveChecked.asp?SUB_PO_NO="+SUB_PO_NO+"&WORKING_CODE="+WORKING_CODE).done(function( data ) {
			//console.log(data);
			
		$("#Result").html(data);
		});
		
	
}


</script>

<body>
<div class="container" align="center">
		<div class="row">


<p align="center"></p> 

<%If not drug.eof then%>
	
	<%If not SMPO.eof then%>

		<table border="0" class="w-100" width ="95%" >
			<tr>
				<td align="left" width ="15%">ใบเบิกเลขที่ :</td>
				<td align="left" width ="15%"><strong><%=SMPO("SUB_PO_NO")%>  </strong> </td>
				<td align="left" width ="10%">หน่วยเบิก  :</td>
				<td align="left" width ="30%"><%=SMPO("DEPT_NAME")%></td>
				<td align="left" width ="15%">วันที่เบิก :</td>
				<td align="left" width ="15%"><%=SMPO("SUB_PO_DATE")%></td>
				
			</tr>
			<tr>
				<td>เวชภัณฑ์ที่เบิก :</td>
				<td colspan="3"><strong><%=SMPO("DRUG_NAME")%></strong> </td>
				<td>คงคลัง :</td>
				<td><%=INVMD("QTY_ON_HAND") & " " & INVMD("SALE_UNIT")%></td>
				
			</tr>
			<tr>
				<td>จำนวนเบิก :</td>
				<td><%=SMPO("QTY_ORDER")%></td>
				<td>จำนวนจ่าย :</td>
				<td><%=SMPO("QTY_RCV")%></td>
				<td> ขนาดบรรจุ :</td>
				<td><%=INVMD("STD_RATIO3") %> </td>
			</tr>

		</table>

		</br>
		<div align="center"><strong> Lot ที่มีในคลังใหญ่</strong> </div>
		<table width="95%" >
			<tr>
				<th width="13%" ><div align="center">Expire date</div></th>
				<th width="10%" ><div align="center">LotNo</div></th>
				<th width="10%" ><div align="center">คงเหลือ</div></th>
				<th width="10%" ><div align="center">ขนาดบรรจุ</div></th>
			</tr>
			<%while not INVMDC.eof%>
			<tr> 
				<td ><div align="center"><%=INVMDC("EXPIRED_DATE")%></div></td>
				<td ><div align="center"><%=INVMDC("LOTNO")%></div></td>
				<td ><div align="center"><%=INVMDC("QTY_ON_HAND")%></div></td>
				<td ><div align="center"><%=INVMDC("PACK_RATIO")%></div></td>
			</tr>
			<%
			INVMDC.movenext
			wend%>
		   

		</table>

		</br>
		<div align="center"><strong> Lot ที่จ่าย </strong> </div>
		<table width="95%" >
			<tr>
				<td width="13%" bgcolor="#FFFF66"><div align="center">Expire date</div></td>
				<td width="10%" bgcolor="#FFFF66"><div align="center">LotNo</div></td>
				<td width="10%" bgcolor="#FFFF66"><div align="center">จำนวนจ่าย</div></td>
				<td width="10%" bgcolor="#FFFF66"><div align="center">ขนาดบรรจุ</div></td>
			</tr>

			<tr> 
				<td bgcolor="#FFFFCC"><div align="center"><strong><%=SMPO("EXPIRED_DATE1")%></strong></div></td>
				<td bgcolor="#FFFFCC"><div align="center"><strong><%=SMPO("LOTNO1")%></strong></div></td>
				<td bgcolor="#FFFFCC"><div align="center"><strong><%=SMPO("QTY_RCV1")%></strong></div></td>
				<td bgcolor="#FFFFCC"><div align="center"><strong><%=SMPO("PACK_RATIO1")%></strong></div></td>
			</tr>
			<%if isnull(SMPO("VENDOR_CODE2")) = false then%>
			<tr> 
				<td bgcolor="#FFFFCC"><div align="center"><strong><%=SMPO("EXPIRED_DATE2")%></strong></div></td>
				<td bgcolor="#FFFFCC"><div align="center"><strong><%=SMPO("LOTNO2")%></strong></div></td>
				<td bgcolor="#FFFFCC"><div align="center"><strong><%=SMPO("QTY_RCV2")%></strong></div></td>
				<td bgcolor="#FFFFCC"><div align="center"><strong><%=SMPO("PACK_RATIO2")%></strong></div></td>
			</tr>
			<%end if%>
			<%if isnull(SMPO("VENDOR_CODE3")) = false then%>
			<tr> 
				<td bgcolor="#FFFFCC"><div align="center"><strong><%=SMPO("EXPIRED_DATE3")%></strong></div></td>
				<td bgcolor="#FFFFCC"><div align="center"><strong><%=SMPO("LOTNO3")%></strong></div></td>
				<td bgcolor="#FFFFCC"><div align="center"><strong><%=SMPO("QTY_RCV3")%></strong></div></td>
				<td bgcolor="#FFFFCC"><div align="center"><strong><%=SMPO("PACK_RATIO3")%></strong></div></td>
			</tr>
			<%end if%>
		   
		</table>
		</br>
			<div>
				<button type ="button" class="btn btn-success btn-sm" onClick="SaveChecked()"> <i class="fas fa-check"></i>ยืนยัน</button> 
				<!--button type ="button" class="btn btn-danger btn-sm" onClick="ShowSMPO()"> <i class="far fa-window-close"></i>ยกเลิก</button--> 
			</div>
			<div id="Result"></div>
	<%else%>
		<div>ไม่พบข้อมูลยาในใบเบิกที่ค้นหา </div>
	<%End if%>
<%else%>

	<div>ไม่พบข้อมูลยาที่ค้นหา </div>

<%End if%>  
  
</body>
</html>
<%

%>
