<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%






Set INVUser= Server.CreateObject("ADODB.Recordset")
INVUser.ActiveConnection = MM_INVFlood_STRING

INVUser.Source = "SELECT UserID, [name] FROM [USER] WHERE (invgroup =4 or invgroup =5) and UserStatus = 'Y'"

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


function ShowSMPO() {
	
	var SUB_PO_NO = document.getElementById("SUB_PO_NO").value;
	var UserID = document.getElementById("UserID").value;
		
	if( SUB_PO_NO == ''){
		
		alert('กรุณาระบุเลขที่ใบเบิก');
	
	}else if( UserID == ''){
	
	alert('กรุณาระบุผู้ตรวจสอบ');
	
	}else{
	
	//sessionStorage.setItem("SUB_PO_NO", SUB_PO_NO);
	
	$.get( "ChkDispDetail.asp?SUB_PO_NO="+SUB_PO_NO+"&UserID="+UserID).done(function( data ) {
			//console.log(data);
			
		$("#SMPOC").html(data);
		});
		
	}
}


</script>


<body>
<div class="container" align="center">
		<div class="row">


<p align="center"></p> 

		<form name="form1" method="GET" action="chkdispdetail.asp">
			<table border="0" class="w-100">
				<tr>
				  <td>รหัสใบเบิก</td>
				  <td><input name="SUB_PO_NO" type="text" id="SUB_PO_NO" class="form-control form-control-sm" /> <br></td>
				</tr>
				<tr>
					<td>ผู้ตรวจสอบ</td>
					<td>
						<select name="UserID" id="UserID" class="form-control form-control-sm">
							<% While (NOT INVUser.EOF)%>
								<option value="<%=(INVUser.Fields.Item("UserID").Value)%>"><%=(INVUser.Fields.Item("name").Value)%></option>
							<%
							INVUser.MoveNext()
							Wend

							%>
						</select>
						<br>
					</td>
				</tr>
				<tr>
				  <td colspan="2" align = "center">
					<button type ="button" class="btn btn-success btn-sm" onClick="ShowSMPO()"> <i class="fab fa-line"></i> ตรวจสอบ </button> 
				  </td>
				</tr>
			</table>
		</form>


<div id="SMPOC"></div>
