<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%



Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.ActiveConnection = MM_INVFlood_STRING

SUB_PO_NO = request.QueryString("SUB_PO_NO")
WORKING_CODE = request.QueryString("WORKING_CODE")

Drug.Source = "SELECT CHECKED FROM SM_PO_C WHERE SUB_PO_NO='" & SUB_PO_NO & "' AND WORKING_CODE='" & WORKING_CODE & "'"
Drug.CursorType = 2
Drug.CursorLocation = 2
Drug.LockType = 3
Drug.Open()



If not drug.eof then

	drug("CHECKED") = "Y" 
	drug.update
	
end if

Set SMPO = Server.CreateObject("ADODB.Recordset")
SMPO.ActiveConnection = MM_INVFlood_STRING
SMPO.Source = "SELECT CHKUSER FROM SM_PO WHERE SUB_PO_NO='" & SUB_PO_NO & "'"
SMPO.CursorType = 2
SMPO.CursorLocation = 2
SMPO.LockType = 3
SMPO.Open()


If not SMPO.eof then

	SMPO("CHKUSER") = Session("UserID") 
	SMPO.update
		
end if

Set SMPOC = Server.CreateObject("ADODB.Recordset")
SMPOC.ActiveConnection = MM_INVFlood_STRING
SMPOC.Source = "SELECT count(WORKING_CODE) as Remain FROM SM_PO_C WHERE SUB_PO_NO='" & SUB_PO_NO & "' AND CHECKED is null"
SMPOC.CursorType = 2
SMPOC.CursorLocation = 2
SMPOC.LockType = 3
SMPOC.Open()



Response.charset="windows-874"

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

  </head>


<body>
<div class="container" align="center">
		<div class="row">


<p align="center"></p> 

<%If not drug.eof then%>
	
	<div>
		บันทึกการตรวจสอบแล้ว คงเหลือรอตรวจสอบ <%=SMPOC("Remain")%> รายการ
	</div>
	
<%End if%>  
  
</body>
</html>
<%

%>
