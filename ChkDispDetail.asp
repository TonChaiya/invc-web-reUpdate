

<!--#include file="Connections/INVFlood.asp" -->

<%


Set SMPO = Server.CreateObject("ADODB.Recordset")
SMPO.ActiveConnection = MM_INVFlood_STRING

SUB_PO_NO = request.QueryString("SUB_PO_NO")
UserID = request.QueryString("UserID")

session("SUB_PO_NO") = SUB_PO_NO
session("UserID") = UserID

SMPO.Source = "SELECT dbo.SM_PO.SUB_PO_NO, dbo.SM_PO.SUB_PO_DATE, dbo.DEPT_ID.DEPT_NAME, dbo.SM_PO_C.WORKING_CODE, dbo.SM_PO_C.QTY_ORDER, dbo.SM_PO_C.QTY_RCV, " &_
			  "dbo.SM_PO_C.PACK_RATIO, dbo.SM_PO_C.CHECKED, dbo.INV_MD.DRUG_NAME, dbo.INV_MD.SALE_UNIT, dbo.INV_MD.WORKING_CODE FROM INV_MD INNER JOIN SM_PO_C ON INV_MD.WORKING_CODE = SM_PO_C.WORKING_CODE " &_
			  "INNER JOIN SM_PO ON SM_PO_C.SUB_PO_NO = SM_PO.SUB_PO_NO INNER JOIN DEPT_ID ON DEPT_ID.DEPT_ID = SM_PO.DEPT_ID WHERE SM_PO.SUB_PO_NO ='" & SUB_PO_NO & "'"

SMPO.CursorType = 3
SMPO.CursorLocation = 3
SMPO.LockType = 1
SMPO.Open()

SMPO_numRows = 0

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

<div>
	ใบเบิกเลขที่ : <strong><%=SMPO("SUB_PO_NO")%>  </strong>  วันที่เบิก : <%=SMPO("SUB_PO_DATE")%> หน่วยเบิก  : <%=SMPO("DEPT_NAME")%>
</div>

<table width="95%" >
  <tr>
    <td width="10%" ><div align="center">ลำดับ</div></td>
    <td width="60%" ><div align="center">รายการเวชภัณฑ์</div></td>
    <td width="10%" ><div align="center">จำนวนเบิก   </div></td>
    <td width="10%" ><div align="center">จำนวนจ่าย</div></td>
    <td width="10%" ><div align="center">ตรวจสอบ</div></td>


  </tr>
  <%  runno = 1
While NOT SMPO.EOF
%>

    <tr> 

		<td ><div align="center"><%=runno%></div></td>
		<td ><div align="left"><%=SMPO("DRUG_NAME")%></div></td>
		<td ><div align="center"><%=SMPO("QTY_ORDER")%></div></td>
		<td ><div align="center"><%=SMPO("QTY_RCV")%></div></td>
		<td ><div align="center"><%=SMPO("CHECKED")%></div></td>
	</tr>
   
<%

  runno = runno +1
  SMPO.MoveNext()
Wend
%>
</table>

  
</body>
</html>
<%
SMPO.Close()
Set SMPO = Nothing
%>
