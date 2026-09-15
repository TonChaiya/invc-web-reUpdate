<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

if request.form("Shelf_ID") <>"" then
Session("Shelf_ID") = request.form("Shelf_ID") 
end if

Dim Drug
Dim Drug_numRows

Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.ActiveConnection = MM_INVFloodCalalog_STRING

Function Urate(Number)
Dim A, B, C, D, E, F, G
		if number = 0 then
		urate = 1 
		else
		urate = number
		end if
End Function

'if session("Shelf_ID") <>"" then 
		Drug.Source =  "SELECT DrugCatalog.HospDrugCode, DrugCatalog.TMTID, DrugCatalog.GenericName, DrugCatalog.TradeName, DrugCatalog.DSFCode, DrugCatalog.DosageForm, DrugCatalog.Strength, DrugCatalog.Content, DrugCatalog.NDC24, DrugCatalog.UnitSize, DrugCatalog.UnitPrice, DrugCatalog.UpdateFlag, DrugCatalog.DateChange, DrugCatalog.DateUpdate, DrugCatalog.DateEffective FROM DrugCatalog;"
'else
'		Drug.Source = "SELECT *  FROM INV_MD WHERE [NOUSE] is NULL" 'order by drug_name "
'end if 

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
<%
Dim Shelf_ID
Dim Shelf_ID_numRows

Set Shelf_ID = Server.CreateObject("ADODB.Recordset")
Shelf_ID.ActiveConnection = MM_INVFlood_STRING
Shelf_ID.Source = "SELECT Location  FROM INV_MD Group by Location order by Location"
Shelf_ID.CursorType = 0
Shelf_ID.CursorLocation = 2
Shelf_ID.LockType = 1
Shelf_ID.Open()

Shelf_ID_numRows = 0
%>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<title>รายการยาและเวชภัณฑ์ตามชั้นเก็บที่ <%=session("Shelf_id")%></title>
<!-- Bootstrap -->
    
    <link rel="stylesheet" href="css/bootstrap.min.css">
    
    <!-- HTML5 shim and Respond.js for IE8 support of HTML5 elements and media queries -->
    <!-- WARNING: Respond.js doesn't work if you view the page via file:// -->
    <!--[if lt IE 9]>
      <script src="https://oss.maxcdn.com/html5shiv/3.7.3/html5shiv.min.js"></script>
      <script src="https://oss.maxcdn.com/respond/1.4.2/respond.min.js"></script>
    <![endif]-->
    <!--#include file="menu.asp" -->
<body>
<div class="container" align="center">
		<div class="row">
<p>
<table width="90%" align="center">
<thead>
  <tr>
    <td><div align="center">
      <form id="form2" name="form2" method="Post" action="shelflist.asp">
        <label>ชั้นวาง/ตู้เย็น
        <select name="Shelf_ID" id="Shelf_ID" onchange ="submit()">
                <%
While (NOT Shelf_ID.EOF)
%>
                <option value="<%=(Shelf_ID.Fields.Item("Location").Value)%>"<%If (Not isNull((Shelf_ID.Fields.Item("Location").Value))) Then If (CStr(Shelf_ID.Fields.Item("Location").Value) = CStr((session("Shelf_id")))) Then Response.Write("selected=""selected""") : Response.Write("")%> ><%=(Shelf_ID.Fields.Item("Location").Value)%></option>
<%
  Shelf_ID.MoveNext()
Wend
If (Shelf_ID.CursorType > 0) Then
  Shelf_ID.MoveFirst
Else
  Shelf_ID.Requery
End If
%>
          </select>
        </label>
        </form>
      </div></td>
  </tr>
</table>


<p>
<table width="95%" class="table" align="center">
	<thead>
  <tr>
    <td width="25%"><div align="center">รหัส&#3618;&#3634; HOMC </div></td>
    <td width="5%"><div align="center">ชื่อสามัญ</div></td>
    <td width="8%"><div align="center">DosageForm </div></td>
    <td width="5%"><div align="center">ความแรง</div></td>
    <td width="7%"><div align="center">&#3627;&#3609;&#3656;&#3623;&#3618;</div></td>
    <td width="5%"><div align="center">รหัส 24 หลัก </div></td>
    <td width="9%"><div align="center">Rate เดือนก่อน </div></td>
    <td width="7%"><div align="center">&#3648;&#3627;&#3621;&#3639;&#3629;&#3651;&#3594;&#3657;&#3652;&#3604;&#3657; (&#3648;&#3604;&#3639;&#3629;&#3609;) </div></td>
  </tr>
  </thead>
  <tbody>
  <%  runno = 1
While ((Repeat1__numRows <> 0) AND (NOT Drug.EOF)) 
%>
<%A= drug("RATE_PER_MONTH") 
		 if  isnumeric(A) = true and isnumeric(drug("QTY_ON_HAND")) = true then
	  	B=  round(drug("QTY_ON_HAND")/drug("RATE_PER_MONTH"),2)
		C = B
				if B<1 and B>0 then
				B = "0" & B
				end if
		
		 else 
		 B = "N/A"
		'C = round(drug("QTY_ON_HAND")/A,2)
		
		end if
%>
    <tr> 
    <td> <%response.write(runno)%>  . <a href="chkstock.asp?code=<%=(Drug.Fields.Item("WORKING_CODE").Value)%>"><%=(Drug.Fields.Item("HospDrugCode").Value)%></a></td>
      	<td><div align="center"><%=(Drug.Fields.Item("GenericName").Value)%> </div></td>
      
    	<td><div align="center"><%=(Drug.Fields.Item("DosageForm").Value)%></div>   </td>

	    <td><div align="center"><%=(Drug.Fields.Item("Strength").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("Content").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("์NDC24").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("RATE_PER_MONTH").Value)%></div></td>
	    <td> <div align="center"> <%response.write(B)%>
<tr>
   
<%
  Repeat1__index=Repeat1__index+1
  at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1
  Drug.MoveNext()
Wend
%>
</tbody>
</table>
<p>&nbsp;</p>

  <script src="css/bootstrap.min.css"></script>
  <script src="js/bootstrap.min.js"></script>
  <script src="js/jquery-3.2.1.min.js"></script>

</body>
</html>
<%
Drug.Close()
Set Drug = Nothing

Shelf_ID.close()
set Shelf_ID = Nothing
%>

