<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

if request.form("DEPT_ID") <>"" then
Session("DEPT_ID") = request.form("DEPT_ID") 
end if

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

if session("DEPT_ID") <>"" and Session("DEPT_ID") <> "00001" then 
		Drug.Source = "SELECT SM_PO.SUB_PO_DATE AS วันที่เบิก, PENDING.SUB_PO_NO AS เลขที่ใบเบิก, PENDING.WORKING_CODE AS รหัสเวชภัณฑ์, INV_MD.DRUG_NAME AS Drug_name, PENDING.PACK_RATIO AS ขนาดบรรจุ, PENDING.QTY_ORDER AS จำนวนเบิก, [QTY_RCV] AS จำนวนจ่าย, PENDING.QTY_PENDING AS จำนวนค้างจ่าย, [USER_PENDING] AS ผู้ค้าง, DEPT_ID.DEPT_ID, PENDING.RECORD_NUMBER, PENDING.CLEAR_PENDING, PENDING.BORROW, (select QTY_ON_HAND FROM INV_MD WHERE QTY_ON_HAND >0 and INV_MD.WORKING_CODE=PENDING.WORKING_CODE) as Qty, INV_MD.VEN, case when INV_MD.VEN='V' then '1' when INV_MD.VEN='E' then '2' else '3' end as Ordering FROM INV_MD INNER JOIN (SM_PO INNER JOIN (DEPT_ID INNER JOIN PENDING ON DEPT_ID.DEPT_ID = PENDING.DEPT_ID) ON SM_PO.SUB_PO_NO = PENDING.SUB_PO_NO) ON INV_MD.WORKING_CODE = PENDING.WORKING_CODE WHERE (PENDING.CLEAR_PENDING) Is Null AND PENDING.DEPT_ID='" &  session("DEPT_ID") & "'ORDER BY case when INV_MD.VEN='V' then '1' when INV_MD.VEN='E' then '2' else '3' end, SM_PO.SUB_PO_DATE"
else
		Drug.Source = "SELECT SM_PO.SUB_PO_DATE AS วันที่เบิก, PENDING.SUB_PO_NO AS เลขที่ใบเบิก, PENDING.WORKING_CODE AS รหัสเวชภัณฑ์, INV_MD.DRUG_NAME AS Drug_name, PENDING.PACK_RATIO AS ขนาดบรรจุ, PENDING.QTY_ORDER AS จำนวนเบิก, [QTY_RCV] AS จำนวนจ่าย, PENDING.QTY_PENDING AS จำนวนค้างจ่าย, [USER_PENDING] AS ผู้ค้าง, DEPT_ID.DEPT_ID, PENDING.RECORD_NUMBER, PENDING.CLEAR_PENDING, PENDING.BORROW, (select QTY_ON_HAND FROM INV_MD WHERE QTY_ON_HAND >0 and INV_MD.WORKING_CODE=PENDING.WORKING_CODE) as Qty, INV_MD.VEN, case when INV_MD.VEN='V' then '1' when INV_MD.VEN='E' then '2' else '3' end as Ordering FROM INV_MD INNER JOIN (SM_PO INNER JOIN (DEPT_ID INNER JOIN PENDING ON DEPT_ID.DEPT_ID = PENDING.DEPT_ID) ON SM_PO.SUB_PO_NO = PENDING.SUB_PO_NO) ON INV_MD.WORKING_CODE = PENDING.WORKING_CODE WHERE (PENDING.CLEAR_PENDING) Is Null ORDER BY case when INV_MD.VEN='V' then '1' when INV_MD.VEN='E' then '2' else '3' end, SM_PO.SUB_PO_DATE"
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
<%
Dim DEPT_ID
Dim DEPT_ID_numRows

Set DEPT_ID = Server.CreateObject("ADODB.Recordset")
DEPT_ID.ActiveConnection = MM_INVFlood_STRING
DEPT_ID.Source = "SELECT *  FROM DEPT_ID  WHERE PENDING='Y' order by DEPT_NAME"
DEPT_ID.CursorType = 0
DEPT_ID.CursorLocation = 2
DEPT_ID.LockType = 1
DEPT_ID.Open()

DEPT_ID_numRows = 0
%>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<title>ระบบค้างจ่ายเวชภัณฑ์</title>
 <!-- Bootstrap -->
    
    <link rel="stylesheet" href="css/bootstrap.min.css">
    
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
        
<table width="90%" border="0" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td colspan="2">&nbsp;</td>
	</tr>
</table>
<table width="90%" border="0" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td><div align="center">
      <form id="form2" name="form2" method="Post" action="pending.asp">
        <label>หน่วยเบิก
        <select name="DEPT_ID" id="DEPT_ID" onchange ="submit()">
                <%
While (NOT DEPT_ID.EOF)
%>
                <option value="<%=(DEPT_ID.Fields.Item("DEPT_ID").Value)%>"<%If (Not isNull((DEPT_ID.Fields.Item("DEPT_ID").Value))) Then If (CStr(DEPT_ID.Fields.Item("DEPT_ID").Value) = CStr((session("DEPT_ID")))) Then Response.Write("selected=""selected""") : Response.Write("")%> ><%=(DEPT_ID.Fields.Item("DEPT_NAME").Value)%></option>
<%
  DEPT_ID.MoveNext()
Wend
If (DEPT_ID.CursorType > 0) Then
  DEPT_ID.MoveFirst
Else
  DEPT_ID.Requery
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
    <td width="12%" <div align="center">รหัสยา</div></td>
    <td width="29%" <div align="center">ชื่อยา</div></td>
    <td width="8%" <div align="center">วันที่เบิก</div></td>
    <td width="8%" <div align="center">จำนวนเบิก</div></td>
    <td width="8%" <div align="center">ค้างจ่าย</div></td>
    <td width="8%" <div align="center">ขนาดบรรจุ</div></td>
    <td width="8%" <div align="center">เลขที่ใบเบิก</div></td>
    <td width="8%" <div align="center">หน่วยเบิก </div></td>
    <td width="5%" <div align="center">ยืมใช้ </div></td>
	<td width="8%" <div align="center">คงคลัง</div></td>
	<td width="8%" <div align="center">VEN</div></td>
  </tr>
  </thead>
  <%  runno = 1
While ((Repeat1__numRows <> 0) AND (NOT Drug.EOF)) 
%>


    <tr   <%if Drug("Qty")> "0" then %> 
		
		style ="background-color:#ccffdd;"
		
	<%end if%>> 
   	  

    <td>  <div align="center"><%response.write(runno)%>  . 
	  <%=(Drug.Fields.Item("รหัสเวชภัณฑ์").Value)%></div></td>
		<td><div align="left"><%=(Drug.Fields.Item("drug_name").Value)%></div></td>
		<td><div align="center"><%=(Drug.Fields.Item("วันที่เบิก").Value)%></div></td>
		<td><div align="center"><%=(Drug.Fields.Item("จำนวนเบิก").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("จำนวนค้างจ่าย").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("ขนาดบรรจุ").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("เลขที่ใบเบิก").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("DEPT_ID").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("BORROW").Value)%></div></td>
		<td><div align="center"><%=(Drug.Fields.Item("Qty").Value)%></div></td>
		<td><div align="center">
			<button 
				<%if Drug("VEN") ="V" then %>
					class="btn btn-danger btn-sm"
				<%elseif Drug("VEN") = "E" then %>
					class="btn btn-warning btn-sm"
				<%else%> 
					class="btn btn-success btn-sm"
				<%end if%>
			><%=(Drug.Fields.Item("VEN").Value)%></button>
		</div></td>
	      <%response.write(B)%>
</tr>
   
<%
  Repeat1__index=Repeat1__index+1
  at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1
  Drug.MoveNext()
Wend
%>
</table>
<p>&nbsp;</p>
</div>
</div>

  <script src="css/bootstrap.min.css"></script>
  <script src="js/bootstrap.min.js"></script>
  <script src="js/jquery-3.2.1.min.js"></script>

</body>
</html>

<%
Drug.Close()
Set Drug = Nothing

DEPT_ID.close()
set DEPT_ID = Nothing
%>

