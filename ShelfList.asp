<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%
' Shelf (ชั้นวาง/ตู้เย็น)
If Request.Form("Shelf_ID") <> "" Then
  If Request.Form("Shelf_ID") = "__ALL__" Then
    Session("Shelf_ID") = ""        ' เลือก “ทั้งหมด” ? ล้างตัวกรอง
  Else
    Session("Shelf_ID") = Request.Form("Shelf_ID")
  End If
  Session("RESP_PER") = ""          ' เลือก shelf แล้ว เคลียร์ตัวกรองผู้รับผิดชอบ
End If

' ผู้รับผิดชอบ
If Request.Form("RESP_PERSON") <> "" Then
  If Request.Form("RESP_PERSON") = "__ALLRESP__" Then
    Session("RESP_PER") = ""        ' เลือก “ทั้งหมด” ? ล้างตัวกรอง
  Else
    Session("RESP_PER") = Request.Form("RESP_PERSON")
  End If
  Session("Shelf_ID") = ""          ' เลือก resp แล้ว เคลียร์ตัวกรอง shelf
End If
%>

<%
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


' ===== เรียงตามชื่อยา (ไทย/อังกฤษ) และกันกรณีชื่อซ้ำด้วยรหัสยา =====
Dim orderBy
orderBy = " ORDER BY DRUG_NAME COLLATE Thai_CI_AS ASC, CAST(WORKING_CODE AS INT) ASC"
' ถ้าฐานไม่รองรับ Thai_CI_AS ค่อยเปลี่ยนเป็น: orderBy = " ORDER BY DRUG_NAME ASC, CAST(WORKING_CODE AS INT) ASC"


If Session("Shelf_ID") <> "" Then
  ' กรองตามชั้นวาง – ไม่ต้อง JOIN เพื่อไม่ให้แถวหาย
  Drug.Source = _
    "SELECT INV_MD.* " & _
    "FROM INV_MD " & _
    "WHERE INV_MD.NOUSE IS NULL " & _
    "  AND LTRIM(RTRIM(INV_MD.Location)) = '" & Replace(Trim(Session("Shelf_ID")), "'", "''") & "'" & _
    orderBy

ElseIf Session("RESP_PER") <> "" Then
  ' กรองตามผู้รับผิดชอบ – JOIN ได้
  Drug.Source = _
    "SELECT INV_MD.*, LOCATION.RESP_PERSON " & _
    "FROM INV_MD INNER JOIN LOCATION " & _
    "  ON LTRIM(RTRIM(INV_MD.LOCATION)) = LTRIM(RTRIM(LOCATION.LOCATION_NAME)) " & _
    "WHERE INV_MD.NOUSE IS NULL " & _
    "  AND LTRIM(RTRIM(LOCATION.RESP_PERSON)) = '" & Replace(Trim(Session("RESP_PER")), "'", "''") & "'" & _
    orderBy

Else
  Drug.Source = "SELECT * FROM INV_MD WHERE NOUSE IS NULL" & orderBy
End If



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

Set RESP = Server.CreateObject("ADODB.Recordset")
RESP.ActiveConnection = MM_INVFlood_STRING
RESP.Source = "SELECT LOCATION.RESP_PERSON FROM LOCATION Group by RESP_PERSON order by RESP_PERSON"
RESP.CursorType = 0
RESP.CursorLocation = 2
RESP.LockType = 1
RESP.Open()


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
				<select name="Shelf_ID" id="Shelf_ID" onchange="submit()">
				'— ตัวเลือก “ทั้งหมด” —
				<option value="__ALL__" <% If Session("Shelf_ID") = "" Then Response.Write("selected=""selected""") End If %> >— ทั้งหมด —</option>

				<% While (NOT Shelf_ID.EOF) %>
					<option value="<%=Shelf_ID("Location")%>"
					<% If (Not IsNull(Shelf_ID("Location"))) Then If CStr(Shelf_ID("Location")) = CStr(Session("Shelf_ID")) Then Response.Write("selected=""selected""") End If End If %>>
					<%=Shelf_ID("Location")%>
					</option>
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

			</div>
		</td>
		<td>
			<form id="RespForm" name="RespForm" method="Post" action="shelflist.asp">
			<label>ผู้รับผิดชอบ
				<select name="RESP_PERSON" id="RESP_PERSON" onchange="submit()">
				<option value="__ALLRESP__" <% If Session("RESP_PER") = "" Then Response.Write("selected=""selected""") End If %> >— ทั้งหมด —</option>

				<% While (NOT RESP.EOF) %>
					<option value="<%=RESP("RESP_PERSON")%>"
					<% If (Not IsNull(RESP("RESP_PERSON"))) Then If CStr(RESP("RESP_PERSON")) = CStr(Session("RESP_PER")) Then Response.Write("selected=""selected""") End If End If %>>
					<%=RESP("RESP_PERSON")%>
					</option>
				<%
					RESP.MoveNext()
				Wend
				%>
				</select>
			</label>
			</form>

		</td>
		<td>
			<a href="shelflist.asp?mode=detail" >แสดง </a>/<a href="shelflist.asp" >ซ่อน</a> รายละเอียด
		</td>
	</tr>
</table>


<p>
<table width="95%" class="table" align="center">
	<thead>
  <tr>
    <td width="2%"></td>
	<td width="25%"><div align="center">&#3594;&#3639;&#3656;&#3629;&#3618;&#3634;</div></td>
    <td width="5%"><div align="center">VEN</div></td>
    <td width="8%"><div align="center">รหัสยา</div></td>
    <td width="5%"><div align="center">&#3588;&#3591;&#3588;&#3621;&#3633;&#3591;</div></td>
    <td width="7%"><div align="center">&#3627;&#3609;&#3656;&#3623;&#3618;</div></td>
    <td width="5%"><div align="center">Location</div></td>
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
		<td><%response.write(runno)%>. </td>
		<td> <a href="chkstock.asp?code=<%=(Drug.Fields.Item("WORKING_CODE").Value)%>"><%=(Drug.Fields.Item("drug_name").Value)%></a></td>
      	
		<td><div align="center"><%=(Drug.Fields.Item("VEN").Value)%> </div></td>
      
    	<td><div align="center"><%=(Drug.Fields.Item("WORKING_CODE").Value)%></div>   </td>

	    <td><div align="center"><%=(Drug.Fields.Item("QTY_ON_HAND").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("SALE_UNIT").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("Location").Value)%></div></td>
	    <td><div align="center"><%=(Drug.Fields.Item("RATE_PER_MONTH").Value)%></div></td>
	    <td> <div align="center"> <%response.write(B)%>
	<tr>
   
	<%
	if request.querystring("mode") = "detail" then 
	
		Set invmdc = Server.CreateObject("ADODB.Recordset")
		invmdc.ActiveConnection = MM_INVFlood_STRING

		invmdc.Source =  "SELECT INV_MD_C.*, DRUG_VN.TRADE_NAME FROM DRUG_VN RIGHT OUTER JOIN INV_MD_C ON DRUG_VN.WORKING_CODE = INV_MD_C.WORKING_CODE AND DRUG_VN.PACK_RATIO = INV_MD_C.PACK_RATIO AND " & _
                        "DRUG_VN.VENDOR_CODE = INV_MD_C.VENDOR_CODE AND DRUG_VN.MANUFAC_CODE = INV_MD_C.MANUFAC_CODE WHERE INV_MD_C.WORKING_CODE='" & drug("WORKING_CODE") & "' order by EXPIRED_DATE , LOTNO DESC"

		invmdc.CursorType = 3
		invmdc.CursorLocation = 3
		invmdc.LockType = 1
		invmdc.Open()
		
		If not invmdc.eof then 
		%>
			<tr> 
			<td></td>
			<td><div align="center">ชื่อการค้า</div></td>
			<td><div align="center">คงคลัง</div></td>
			<td><div align="center">ขนาดบรรจุ</div>   </td>
			<td><div align="center">วันหมดอายุ</div></td>
			<td><div align="center">Lot No.</div></td>
			<td><div align="center">Location</div></td>
			<td><div align="center">จ่่ายก่อน</div></td>
			<td> <div align="center"> 
		</tr>
		
		<%
			while not invmdc.eof
		%>
		<tr> 
			<td></td>
			<td><div align="left"><%=invmdc("TRADE_NAME")%></td>
			<td><div align="center"><%=invmdc("QTY_ON_HAND")%> </div></td>
		  
			<td><div align="center"><%=invmdc("PACK_RATIO")%></div>   </td>

			<td><div align="center"><%=invmdc("EXPIRED_DATE")%></div></td>
			<td><div align="center"><%=invmdc("LOTNO")%></div></td>
			<td><div align="center"><%=invmdc("LOCATION")%></div></td>
			<td><div align="center"><%=invmdc("DISP_FIRST")%></div></td>
			<td> <div align="center"> 
		</tr>
		<%	
	   
			invmdc.movenext
			wend
			
		end if 		
	end if
	%>
   
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

RESP.Close()
Set RESP = Nothing

%>

