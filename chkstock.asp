<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Function CVNull(Number)
	
	If IsNull(Number) = True Or Number = "" Then
		CVNull = 0
	else 
		CVNull = Number
	end if 

end function 

function ShowPatientName(RECEIVE_NO)

	Set ShowPTname = Server.CreateObject("ADODB.Recordset")
	ShowPTname.ActiveConnection = MM_INVFlood_STRING
	
	ShowPTname.Source = "select PO_C_NOTE from MS_IVO inner join MS_IVO_C on MS_IVO.RECEIVE_NO=MS_IVO_C.RECEIVE_NO inner join MS_PO on MS_IVO.PO_NO = MS_PO.REAL_PO inner join MS_PO_C on MS_PO.PO_NO=MS_PO_C.PO_NO where MS_IVO.RECEIVE_NO ='" & RECEIVE_NO & "'" 
  
	ShowPTname.CursorType = 3
	ShowPTname.CursorLocation = 3
	ShowPTname.LockType = 1
	ShowPTname.Open()
	
	if not ShowPTname.eof then 	
		if isnull(ShowPTname("PO_C_NOTE")) = false then 
			ShowPatientName = "(" & ShowPTname("PO_C_NOTE") & ")"
		else 
			'ShowPatientName = RECEIVE_NO 
		end if
	else 
		'ShowPatientName = RECEIVE_NO		
	end if 

end function 

Dim Drug
Dim Drug_numRows

Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.ActiveConnection = MM_INVFlood_STRING

keyword = request.QueryString("search")
Drug.Source = "SELECT *  FROM DRUG_VN WHERE ([BAR_CODE] Like '%" + Replace(keyword, "'", "''") + "%')"'

'keyword = request.QueryString("search")
'Drug.Source = "SELECT *  FROM INV_MD WHERE ([nouse] is Null AND ([drug_name] + [composition] + [working_code] + [Group_CODE] Like '%" + Replace(keyword, "'", "''") + "%'))"' order by ([QTY_ON_HAND]/[RATE_PER_MONTH])"

Drug.CursorType = 3
Drug.CursorLocation = 3
Drug.LockType = 1
Drug.Open()

Drug_numRows = 0

Dim INVMD
Dim INVMD_numRows

Set INVMD = Server.CreateObject("ADODB.Recordset")
INVMD.ActiveConnection = MM_INVFlood_STRING

if request.QueryString("search")= "" then 
	code = request.QueryString("code")
else
	code = drug("working_code")
end if 

INVMD.Source = "SELECT *  FROM INV_MD WHERE WORKING_CODE='" & code & "'"

INVMD.CursorType = 3
INVMD.CursorLocation = 3
INVMD.LockType = 1
INVMD.Open()

INVMD_numRows = 0

Dim INVMDC
Dim INVMDC_numRows

Set INVMDC= Server.CreateObject("ADODB.Recordset")
INVMDC.ActiveConnection = MM_INVFlood_STRING

INVMDC.Source = "SELECT MD.*,D.TRADE_NAME,C1.COMPANY_NAME as MANU_NAME, C2.COMPANY_NAME as VENDOR_NAME FROM INV_MD_C MD LEFT JOIN DRUG_VN D ON MD.WORKING_CODE = D.WORKING_CODE AND MD.PACK_RATIO = D.PACK_RATIO AND MD.VENDOR_CODE = D.VENDOR_CODE AND MD.MANUFAC_CODE = D.MANUFAC_CODE LEFT JOIN COMPANY C1 ON D.MANUFAC_CODE = C1.COMPANY_CODE LEFT JOIN COMPANY C2 ON D.VENDOR_CODE = C2.COMPANY_CODE WHERE MD.WORKING_CODE='" & code & "'"
'INVMDC.Source = "SELECT *  FROM INV_MD_C WHERE WORKING_CODE='" & code & "'"

INVMDC.CursorType = 3
INVMDC.CursorLocation = 3
INVMDC.LockType = 1
INVMDC.Open()

INVMDC_numRows = 0

Dim Repeat1__numRows
Dim Repeat1__index

Repeat1__numRows = -1
Repeat1__index = 0
INVMDC_numRows = INVMDC_numRows + Repeat1__numRows

Dim CARD
Dim CARD_numRows

Set CARD= Server.CreateObject("ADODB.Recordset")
CARD.ActiveConnection = MM_INVFlood_STRING

CARD.Source = "SELECT *  FROM CARD WHERE WORKING_CODE='" & code & "' ORDER BY RECORD_NUMBER DESC"

CARD.CursorType = 3
CARD.CursorLocation = 3
CARD.LockType = 1
CARD.Open()

CARD_numRows = 0

Dim CARDSUBS
Dim CARDSUBS_numRows

Set CARDSUBS= Server.CreateObject("ADODB.Recordset")
CARDSUBS.ActiveConnection = MM_INVFlood_STRING

CARDSUBS.Source = "SELECT *  FROM CARD_SUBS WHERE WORKING_CODE='" & code & "' ORDER BY RECORD_NUMBER DESC"

CARDSUBS.CursorType = 3
CARDSUBS.CursorLocation = 3
CARDSUBS.LockType = 1
CARDSUBS.Open()

CARDSUBS_numRows = 0



Dim Repeat2__numRows
Dim Repeat2__index

Repeat2__numRows = -1
Repeat2__index = 0
CARD_numRows = CARD_numRows + Repeat2__numRows


Dim SUBSTOCK
Dim SUBSTOCK_numRows

Set SUBSTOCK = Server.CreateObject("ADODB.Recordset")
SUBSTOCK.ActiveConnection = MM_INVFlood_STRING

SUBSTOCK.Source = "SELECT *  FROM SUBSTOCK WHERE WORKING_CODE='"  & code & "' order by  RECORD_NUMBER DESC"

SUBSTOCK.CursorType = 3
SUBSTOCK.CursorLocation = 3
SUBSTOCK.LockType = 1
SUBSTOCK.Open()

SUBSTOCK_numRows = 0

Dim Repeat3__numRows
Dim Repeat3__index

Repeat3__numRows = -1
Repeat3__index = 0
SUBSTOCK_numRows = SUBSTOCK_numRows + Repeat3__numRows

Dim Repeat4__numRows
Dim Repeat4__index

Repeat4__numRows = -1
Repeat4__index = 0
CARDSUBS_numRows = CARDSUBS_numRows + Repeat4__numRows
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

<body>
<div class="container" align="center">
		<div class="row">


<p align="center"></p> 

<table width="95%" >
  <tr>
    <td width="10%" bgcolor="#FFFF66"><div align="center">รหัสยา</div></td>
    <td width="13%" bgcolor="#FFFFCC"><div align="center"><span class="style2"><strong><%=(INVMD.Fields.Item("WORKING_CODE").Value)%></strong></span></div></td>
    <td width="10%" bgcolor="#FFFF66"><div align="center">&#3594;&#3639;&#3656;&#3629;&#3618;&#3634;<br />
    </div></td>
    <td width="25%" bgcolor="#FFFFCC"><div align="center"><strong><%=(INVMD.Fields.Item("drug_name").Value)%></strong></div></td>
    <td width="10%" bgcolor="#FFFF66"><div align="center">Location</div></td>
    <td width="12%" bgcolor="#FFFFCC"><div align="center"><%=(INVMD.Fields.Item("Location").Value)%></div></td>

  </tr>
  <%  runno = 1
'While ((Repeat1__numRows <> 0) AND (NOT INVMD.EOF)) 
%>

    <tr> 

    <td bgcolor="#FFFF66"><div align="center">คงคลัง<br />
    </div></td>
      <td bgcolor="#f56c62"><div align="center"><strong><%=CLNG(INVMD.Fields.Item("QTY_ON_HAND").Value)/CLNG(INVMD.Fields.Item("STD_RATIO3").Value)%></strong></div></td>
      
    <td bgcolor="#FFFF66"><div align="center">ขนาดบรรจุ</div>   </td>
	    <td bgcolor="#FFFFCC"><div align="center">
	      <div align="center"><%=(INVMD.Fields.Item("STD_RATIO3").Value)%></div>
	    </div></td>
	    <td bgcolor="#FFFF66"><div align="center">หน่วย</div></td>
	    <td bgcolor="#FFFFCC"><div align="center"><%=(INVMD.Fields.Item("SALE_UNIT").Value)%></div></td>


</tr>
   
<%
 ' Repeat1__index=Repeat1__index+1
  'at1__index=Repeat1__index+1
  'Repeat1__numRows=Repeat1__numRows-1
  'runno = runno +1
  'INVMD.MoveNext()
'Wend
%>
</table>
</br>
<p align="center">รายละเอียดยาที่อยู่ในคลังใหญ่</p>



<table width="95%" >
  <tr>
    <td width="3%" bgcolor="#FFFF66"><div align="center">ลำดับ</div></td>
    <td width="25%" bgcolor="#FFFF66"><div align="center">ชื่อการค้า</div></td>
    <td width="6%" bgcolor="#FFFF66"><div align="center">คงเหลือ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">ขนาดบรรจุ</div></td>
    <td width="9%" bgcolor="#FFFF66"><div align="center">วันหมดอายุ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">เลขที่ผลิต</div></td>
    <td width="5%" bgcolor="#FFFF66"><div align="center">ที่เก็บ</div></td>
    <td width="15%" bgcolor="#FFFF66"><div align="center">ผู้จำหน่าย</div></td>
        <td width="15%" bgcolor="#FFFF66"><div align="center">ผู้ผลิต</div></td>
  </tr>
  <%  runno = 1
While ((Repeat1__numRows <> 0) AND (NOT INVMDC.EOF)) 
%>
<%
' Dim DVN
' Dim DVN_numRows

' Set DVN = Server.CreateObject("ADODB.Recordset")
' DVN.ActiveConnection = MM_INVFlood_STRING

' DVN.Source = "SELECT MD.*,D.TRADE_NAME,C1.COMPANY_NAME as MANU_NAME, C2.COMPANY_NAME as VENDOR_NAME FROM INV_MD_C MD LEFT JOIN DRUG_VN D ON MD.WORKING_CODE = D.WORKING_CODE AND MD.PACK_RATIO = D.PACK_RATIO AND MD.VENDOR_CODE = D.VENDOR_CODE AND MD.MANUFAC_CODE = D.MANUFAC_CODE LEFT JOIN COMPANY C1 ON D.MANUFAC_CODE = C1.COMPANY_CODE LEFT JOIN COMPANY C2 ON D.VENDOR_CODE = C2.COMPANY_CODE WHERE MD.WORKING_CODE='" & INVMDC("WORKING_CODE") & "'" 
' 'DVN.Source = "SELECT *  FROM DRUG_VN WHERE WORKING_CODE='" & INVMDC("WORKING_CODE") & "' AND VENDOR_CODE='" & INVMDC("VENDOR_CODE") & "' AND MANUFAC_CODE='" & INVMDC("MANUFAC_CODE") & "' AND PACK_RATIO=" & INVMDC("PACK_RATIO")

' DVN.CursorType = 3
' DVN.CursorLocation = 3
' DVN.LockType = 1
' DVN.Open()

' DVN_numRows = 0

' Dim VENDOR
' Dim VENDOR_numRows

' Set VENDOR = Server.CreateObject("ADODB.Recordset")
' VENDOR.ActiveConnection = MM_INVFlood_STRING

' VENDOR.Source = "SELECT * FROM COMPANY WHERE COMPANY_CODE='" & DVN("VENDOR_CODE") & "'"

' VENDOR.CursorType = 3
' VENDOR.CursorLocation = 3
' VENDOR.LockType = 1
' VENDOR.Open()

' VENDOR_numRows = 0

' Dim MANUFAC
' Dim MANUFAC_numRows

' Set MANUFAC = Server.CreateObject("ADODB.Recordset")
' MANUFAC.ActiveConnection = MM_INVFlood_STRING

' MANUFAC.Source = "SELECT * FROM COMPANY WHERE COMPANY_CODE='" & DVN("MANUFAC_CODE") & "'"

' MANUFAC.CursorType = 3
' MANUFAC.CursorLocation = 3
' MANUFAC.LockType = 1
' MANUFAC.Open()

' MANUFAC_numRows = 0

%>
    <tr> 

    <td valign="middle" bgcolor="#FFFFCC">  <div align="center">
      <%response.write(runno)%>     </div></td>
            <td bgcolor="#FFFFCC"><div align="left"><%=(INVMDC.Fields.Item("TRADE_NAME").Value)%></div></td>
    
    	<% dim A, B, C
		A = CLng(CVnull(INVMDC.Fields.Item("QTY_ON_HAND").Value))
		B = CLng(CVnull(INVMDC.Fields.Item("PACK_RATIO").Value)) 
		
		if B <> 0 then
			C = A/B 
		end if
		%>  
      <td bgcolor="#FFFFCC"><div align="center"><%=C%></div></td>
      
    <td bgcolor="#FFFFCC"><div align="center"><span class="style2"> <%=(INVMDC.Fields.Item("PACK_RATIO").Value)%> </span></div>   </td>
	    <td bgcolor="#FFFFCC"><div align="center"><%=(INVMDC.Fields.Item("EXPIRED_DATE").Value)%></div></td>
	    <td bgcolor="#FFFFCC"><div align="center"><%=(INVMDC.Fields.Item("LOTNO").Value)%></div></td>
	    <td bgcolor="#FFFFCC"><div align="center"><%=(INVMDC.Fields.Item("Location").Value)%></div></td>

        <td bgcolor="#FFFFCC"><div align="center"><%=(INVMDC.Fields.Item("VENDOR_NAME").Value)%></div></td>
        <td bgcolor="#FFFFCC"><div align="center"><%=(INVMDC.Fields.Item("MANU_NAME").Value)%></div></td>

</tr>
   
<%
  Repeat1__index=Repeat1__index+1
  at1__index=Repeat1__index+1
  Repeat1__numRows=Repeat1__numRows-1
  runno = runno +1
  INVMDC.MoveNext()
Wend
%>
</table>


</br>
<p align="center">รายละเอียดยาที่อยู่ในคลังย่อย </p>



<table width="95%" >
  <tr>
    <td width="3%" bgcolor="#FFFF66"><div align="center">ลำดับ</div></td>
    <td width="25%" bgcolor="#FFFF66"><div align="center">หน่วยงาน</div></td>
    <td width="6%" bgcolor="#FFFF66"><div align="center">คงเหลือ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">ขนาดบรรจุ</div></td>
    <td width="9%" bgcolor="#FFFF66"><div align="center">มูลค่า</div></td>
    <td width="5%" bgcolor="#FFFF66"><div align="center">ที่เก็บ</div></td>
  </tr>
 
<%  runno = 1
if not SUBSTOCK.eof then
While ((Repeat3__numRows <> 0) AND (NOT SUBSTOCK.EOF)) 
%>
<%


Dim SUBDPT
Dim SUBDPT_numRows

Set SUBDPT = Server.CreateObject("ADODB.Recordset")
SUBDPT.ActiveConnection = MM_INVFlood_STRING

'SUBDPT.Source = "SELECT *  FROM DEPT_ID WHERE DEPT_ID='" & CARDSUBS("DEPT_ID") & "'"
SUBDPT.Source = "SELECT *  FROM DEPT_ID WHERE DEPT_ID='" & SUBSTOCK("DEPT_ID") & "'"

SUBDPT.CursorType = 3
SUBDPT.CursorLocation = 3
SUBDPT.LockType = 1
SUBDPT.Open()

SUBDPT_numRows = 0


%>
	<tr> 
		 <td valign="middle" bgcolor="#FFFFCC">  <div align="center"><%response.write(runno)%>     </div></td>
		<td bgcolor="#FFFFCC"><div align="left"><a href="seesubstock.asp?code=<%=(SUBSTOCK.Fields.Item("WORKING_CODE").Value)%>&DEPT_ID=<%=(SUBSTOCK.Fields.Item("DEPT_ID").Value)%>" target=_blank"><%=(SUBDPT.Fields.Item("DEPT_NAME").Value)%></a></div></td>
    	<% dim D, E, F
		D = CLng(CVNull(SUBSTOCK.Fields.Item("QTY_ON_HAND").Value))
		E = CLng(CVNull(SUBSTOCK.Fields.Item("PACK_RATIO").Value)) 
		if E <> 0 then 
			F = D/E
		end if 
		%>  
      <td bgcolor="#FFFFCC"><div align="center"><%=F%></div></td>
	  <td bgcolor="#FFFFCC"><div align="center"><%=(SUBSTOCK.Fields.Item("PACK_RATIO").Value)%></div></td>
	  <td bgcolor="#FFFFCC"><div align="center"><%=(SUBSTOCK.Fields.Item("TOTAL_VALUE").Value)%></div></td>
      <td bgcolor="#FFFFCC"><div align="center"><%=(SUBSTOCK.Fields.Item("LOCATION").Value)%></div></td>
  </tr>
<%
  Repeat3__index=Repeat3__index+1
  at3__index=Repeat3__index+1
  Repeat3__numRows=Repeat3__numRows-1
  runno = runno +1
  'END IF
  SUBSTOCK.MoveNext()
Wend
end if
%>
</table>

<!-------------- hide table sub_stock ------------>

<!--

<p align="center">ข้อมูลการเบิกจ่ายของคลังย่อย (แสดง 10 รายการล่าสุด)</p>
<table width="95%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="3%" bgcolor="#FFFF66"><div align="center">ลำดับ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">วันที่ทำรายการ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">เบิก/จ่าย</div></td>
    <td width="18%" bgcolor="#FFFF66"><div align="center">หน่วยเบิก</div></td>
    <td width="7%" bgcolor="#FFFF66"><div align="center">จำนวนเบิก/จ่าย</div></td>
        <td width="7%" bgcolor="#FFFF66"><div align="center">ขนาดบรรจุ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">จำนวนคงเหลือ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">เลขที่เอกสาร</div></td>


  </tr>
  <%  runno = 1
Do While ((Repeat4__numRows <> 0) AND (NOT CARDSUBS.EOF)) 
%>

  <tr>
    <td valign="middle" bgcolor="#FFFFCC"><div align="center">
      <%response.write(runno)%>
    </div></td>
    <td bgcolor="#FFFFCC"><div align="left"><%=(CARDSUBS.Fields.Item("OPERATE_DATE").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=(CARDSUBS.Fields.Item("R_S_STATUS").Value)%></div></td>
	<td bgcolor="#FFFFCC"><div align="center"><%=(CARDSUBS.Fields.Item("R_S_STATUS").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=CLNG(CARDSUBS.Fields.Item("R_S_QTY").Value)/CLNG(INVMD.Fields.Item("STD_RATIO3").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=CLNG(INVMD.Fields.Item("STD_RATIO3").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=CLNG(CARDSUBS.Fields.Item("REMAIN_QTY").Value)/CLNG(INVMD.Fields.Item("STD_RATIO3").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=(CARDSUBS.Fields.Item("R_S_NUMBER").Value)%></div></td>
  </tr>
  <%
  Repeat4__index=Repeat4__index+1
  at4__index=Repeat4__index+1
  Repeat4__numRows=Repeat4__numRows-1
  runno = runno +1
  if runno>10 then
  exit do
  end if 
  CARDSUBS.MoveNext()
Loop
%>
</table>

-->
</br>
<p align="center">ข้อมูลการเบิกจ่าย (แสดง 100 รายการล่าสุด)</p>
<table width="95%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="3%" bgcolor="#FFFF66"><div align="center">ลำดับ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">วันที่ทำรายการ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">เบิก/จ่าย</div></td>
    <td width="18%" bgcolor="#FFFF66"><div align="center">หน่วยเบิก</div></td>
    <td width="7%" bgcolor="#FFFF66"><div align="center">จำนวนเบิก/จ่าย</div></td>
        <td width="7%" bgcolor="#FFFF66"><div align="center">ขนาดบรรจุ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">จำนวนคงเหลือ</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">เลขที่เอกสาร</div></td>


  </tr>
  <%  runno = 1
Do While ((Repeat2__numRows <> 0) AND (NOT CARD.EOF)) 

Dim DPT
Dim DPT_numRows

Set DPT = Server.CreateObject("ADODB.Recordset")
DPT.ActiveConnection = MM_INVFlood_STRING

DPT.Source = "SELECT *  FROM DEPT_ID WHERE DEPT_ID='" & CARD("DEPT_ID") & "'"

DPT.CursorType = 3
DPT.CursorLocation = 3
DPT.LockType = 1
DPT.Open()

DPT_numRows = 0


Dim CPY
Dim CPY_numRows

Set CPY = Server.CreateObject("ADODB.Recordset")
CPY.ActiveConnection = MM_INVFlood_STRING

CPY.Source = "SELECT * FROM COMPANY WHERE COMPANY_CODE='" & CARD("DEPT_ID") & "'"

CPY.CursorType = 3
CPY.CursorLocation = 3
CPY.LockType = 1
CPY.Open()

CPY_numRows = 0




%>

  <tr>
    <td valign="middle" bgcolor="#FFFFCC"><div align="center">
      <%response.write(runno)%>
    </div></td>
    <td bgcolor="#FFFFCC"><div align="left"><%=(CARD.Fields.Item("OPERATE_DATE").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=(CARD.Fields.Item("R_S_STATUS").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><span class="style2"> 
	<%
	If CARD.Fields.Item("R_S_STATUS").Value = "S" then 
		if not DPT.EOF Then
			response.write (DPT.Fields.Item("DEPT_NAME").Value)
		else 
			response.write (CPY.Fields.Item("COMPANY_NAME").Value)
		end if 
	elseif CARD.Fields.Item("R_S_STATUS").Value = "R" then 
		'response.write CARD("RECORD_NUMBER")
		'response.write (CPY.Fields.Item("COMPANY_NAME").Value)
		if not CPY.EOF Then
			response.write CPY.Fields.Item("COMPANY_NAME").Value & ShowPatientName(CARD.Fields.Item("R_S_NUMBER").Value)
		else 
			response.write (DPT.Fields.Item("DEPT_NAME").Value)
		end if
	end if 	
	%>
    </span></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=(CLNG(CARD.Fields.Item("ACTIVE_QTY1").Value) + CLNG(CARD.Fields.Item("ACTIVE_QTY2").Value)+ CLNG(CARD.Fields.Item("ACTIVE_QTY3").Value))/CLNG(INVMD.Fields.Item("STD_RATIO3").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=CLNG(INVMD.Fields.Item("STD_RATIO3").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=CLNG(CARD.Fields.Item("REMAIN_QTY").Value)/CLNG(INVMD.Fields.Item("STD_RATIO3").Value)%></div></td>
    <td bgcolor="#FFFFCC"><div align="center"><%=CARD.Fields.Item("R_S_NUMBER").Value%></div></td>
  </tr>
  <%
  Repeat2__index=Repeat2__index+1
  at2__index=Repeat2__index+1
  Repeat2__numRows=Repeat2__numRows-1
  runno = runno +1
  if runno>100 then
  exit do
  end if 
  CARD.MoveNext()
Loop
%>
</table>
<p>&nbsp;</p>

	  </div>
  </div>
  
</body>
</html>
<%
Drug.Close()
Set Drug = Nothing
%>
