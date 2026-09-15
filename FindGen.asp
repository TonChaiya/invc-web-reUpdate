<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>
<!--#include file="Connections/homc.asp" -->
<%
Dim Drug
Dim Drug_numRows
search = request.QueryString("search") 
Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.ActiveConnection = MM_homc_STRING
Drug.Source = "SELECT dbo.Med_inv.abbr, dbo.Med_inv.code, dbo.Med_inv.name, dbo.Med_inv.dform, dbo.Med_inv.hideSelect, case when trim(dbo.Med_inv.prod_type_ac)='Y' then 'งานผลิตเตรียม' end as Prod FROM dbo.Med_inv WHERE site='1' and dbo.Med_inv.abbr like '%" & ucase(search) & "%'"
Drug.CursorType = 3
Drug.CursorLocation = 3
Drug.LockType = 1
Drug.Open()

Drug_numRows = 0
%>

<%
Dim Repeat1__numRows
Dim Repeat1__index

Repeat1__numRows = -1
Repeat1__index = 0
Drug_numRows = Drug_numRows + Repeat1__numRows
%>


<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<title>ค้นหาชื่อสามัญทางยา (Generic name) สำหรับตั้งรหัสยาใหม่ใน HOMC</title>
 
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

<body>

<div class="container" align="center">
		<div class="row">
 
 
 <p align="center" >ค้นหาชื่อย่อ Abbr (Generic name) สำหรับตั้งรหัสยาใหม่ใน HOMC </p>
 
<form id="form1" name="form1" method="get" action="findgen.asp">
      <label>
      <div align="center">
        <input name="Search" type="text" id="Search" size="30" placeholder="รหัสยา ชื่อยา HOMC"/> 
        <input type="submit" class="btn btn-success" name="Submit" value="ค้นหา" />
        </label>
      </div>
    </form>          
    
<p align="center"> (หากพบข้อมูลให้ copy ชื่อย่อ Abbr ไปใช้ในการตั้งรหัส HOMC)</p>

<p align="center">&nbsp;</p> 
<p><table width="95%" class = "table">
  <tr>
    <td width="38%" bgcolor="#FFFF66"><div align="center">ชื่อย่อ (Abbr) สำหรับใช้ตรวจสอบการแพ้ยา </div></td>
    <td width="33%" bgcolor="#FFFF66"><div align="center">ชื่อยา HOMC </div></td>
    <td width="10%" bgcolor="#FFFF66"><div align="center">รหัสยา HOMC </div></td>
    <td width="10%" bgcolor="#FFFF66"><div align="center">Dosage form </div></td>
	<td width="10%" bgcolor="#FFFF66"><div align="center">หมายเหตุ </div></td>
  </tr>
  <%  runno = 1
While ((Repeat1__numRows <> 0) AND (NOT Drug.EOF)) 
%>
    <tr> 

    <td>  <%response.write(runno)%>  . <%=(Drug.Fields.Item("abbr").Value)%></td>

	    <td><div align="center"><span class="style2">
	      </span>
	        <div align="left"><span class="style2">
	          </span>
	          <div align="center"><span class="style2">
	            <div align="left"><%=(Drug.Fields.Item("name").Value)%></div>
	            </span></div>
            <span class="style2"></span></div>
	        <span class="style2"></span></div></td>
	    
	    <td><div align="center"><%=(Drug.Fields.Item("code").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("dform").Value)%></div></td>
		<td><div align="center"><%=(Drug.Fields.Item("prod").Value)%></div></td>

<tr>
   
<%
  Repeat1__index=Repeat1__index+1
at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1
  Drug.MoveNext()
Wend
%>
</table>
</p>
  </div>
</div>
  
</body>
</html>
<%
Drug.Close()
Set Drug = Nothing
%>

