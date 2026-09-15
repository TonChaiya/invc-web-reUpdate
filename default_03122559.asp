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

if request.QueryString("mode") ="Red" then  'หมด
'		Drug.Source = "SELECT *  FROM drug WHERE NoUse =0 and STANDARD_CODE not like 'O%' and ([QTY_ON_HAND]/[rate1]) =0 and [PO_NO]=[InvNO] and [invoicedate]<[receivedate] order by drug_name"
'elseif request.QueryString("mode") ="Pink" then  '<0.5 
'		Drug.Source = "SELECT *  FROM drug WHERE NoUse =0 and STANDARD_CODE not like 'O%' and [QTY_ON_HAND]<> 0 and ([QTY_ON_HAND]/[rate1]) <0.5 and [PO_NO]=[InvNO] and [invoicedate]<[receivedate] order by  ([QTY_ON_HAND]/[rate1])"
'elseif request.QueryString("mode") ="Blue" then  '0.5 -1 
'		Drug.Source = "SELECT *  FROM drug WHERE NoUse =0 and STANDARD_CODE not like 'O%' and ([QTY_ON_HAND]/[rate1])>= 0.5 and ([QTY_ON_HAND]/[rate1]) <=1 and [PO_NO]=[InvNO] and [invoicedate]<[receivedate]  order by  ([QTY_ON_HAND]/[rate1])"
'elseif request.QueryString("mode") ="Green" then  '>1 
'		Drug.Source = "SELECT *  FROM drug WHERE NoUse =0 and STANDARD_CODE not like 'O%' and ([QTY_ON_HAND]/[rate1])>1 and [PO_NO]=[InvNO] and [invoicedate]<[receivedate] order by  ([QTY_ON_HAND]/[rate1])"
'elseif request.QueryString("mode") ="antidote" then  
'	Drug.Source = "SELECT *  FROM drug WHERE antidote =1 order by  DRUG_NAME"

'elseif request.Querystring("select") = "V" then 
'		Drug.Source = "SELECT *  FROM drug WHERE mid([STANDARD_CODE],2,1)='V' and [PO_NO]=[InvNO] and [invoicedate]<[receivedate] order by [QTY_ON_HAND]/[rate1]"
		
'elseif request.Querystring("select") = "E" then 
'		Drug.Source = "SELECT *  FROM drug WHERE mid([STANDARD_CODE],2,1)='E' and [PO_NO]=[InvNO] and [invoicedate]<[receivedate] order by [QTY_ON_HAND]/[rate1]"

'elseif request.Querystring("select") = "N" then 
'		Drug.Source = "SELECT *  FROM drug WHERE mid([STANDARD_CODE],2,1)='N' and [PO_NO]=[InvNO] and [invoicedate]<[receivedate] order by [QTY_ON_HAND]/[rate1]"
		
elseif request.QueryString("search") <> "" then 
		keyword = request.QueryString("search")
		Drug.Source = "SELECT *  FROM INV_MD WHERE ([nouse] is Null AND ([drug_name] + [composition] + [working_code] + [Group_CODE] + [HOSP_CODE] Like '%" + Replace(keyword, "'", "''") + "%'))"' order by ([QTY_ON_HAND]/[RATE_PER_MONTH])"
		'Drug.Source = "SELECT *  FROM INV_MD WHERE ([nouse] is Null AND ([drug_name] + [composition] + [working_code] + [Group_CODE] + [HOSP_CODE] Like '%" +  keyword + "%'))"' order by ([QTY_ON_HAND]/[RATE_PER_MONTH])"
		'Drug.Source = "SELECT *  FROM INV_MD WHERE ([nouse] is Null AND ([drug_name] + [composition] + [working_code] + [Group_CODE] + [HOSP_CODE] Like '%" + Lcase(Replace(keyword, "'", "''")) + "%'))"' order by ([QTY_ON_HAND]/[RATE_PER_MONTH])"
else
		Drug.Source = "SELECT *  FROM INV_MD WHERE [NOUSE] is NULL" 'order by drug_name "
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

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=windows-874" />
<title>ระบบรายงานปริมาณยาและเวชภัณฑ์คงคลัง</title>
<style type="text/css">
<!--
.style1 {
	font-size: 24px;
	color: #0000FF;
}
.style6 {color: #0000FF}
.style7 {color: #FF0000}
.style8 {color: #FF99FF}
.style9 {color: #99FFFF}
.style10 {color: #66FF66}
-->
</style>
<!--#include file="menu.asp" -->
</head>

<body>

<br>
<br>
</div>
<table width="90%" border="0" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td colspan="2"><div align="center" class="style1">&#3619;&#3632;&#3610;&#3610;&#3619;&#3634;&#3618;&#3591;&#3634;&#3609;&#3611;&#3619;&#3636;&#3617;&#3634;&#3603;&#3618;&#3634;&#3649;&#3621;&#3632;&#3648;&#3623;&#3594;&#3616;&#3633;&#3603;&#3601;&#3660;&#3588;&#3591;ค&#3621;&#3633;&#3591;</div><br></td>
  </tr>
  <tr>
  </tr>
  <tr>
    <td colspan="2"><div align="center"></div></td>
  </tr>
  <tr>
    <td colspan="2"><form id="form1" name="form1" method="get" action="default.asp">
      <label>
      <div align="center">ค้นหาข้อมูลยา 
        <input name="Search" type="text" id="Search" size="30" /> 
        <input type="submit" name="Submit" value="Submit" />
      </div>
      </label>
        <label>
        <div align="center">(สามารถค้นหาได้จาก รหัสยา ชื่อสามัญทางยา ชื่อการค้า และกลุ่มยา)
        </div>
        <div align="center"></div>
        <div align="center"></div>
        <div align="center"></div>
        </label>
    </form>    </td>
  </tr>
</table>

<p align="center"> <span class="style6"><% if keyword <>"" then
response.write("ผลการค้นหา ( ")
response.write(keyword) 
response.write(" ) พบ ")
response.write(drug.recordcount) 
response.write(" รายการ")
end if
%>
 
</span></p> 
<div align="center"><a href="default_print.asp"></a><br>
</div>
<table width="95%" border="1" align="center" cellpadding="0" cellspacing="0">
  <tr>
    <td width="25%" bgcolor="#FFFF66"><div align="center">&#3594;&#3639;&#3656;&#3629;&#3618;&#3634;</div></td>
    <td width="5%" bgcolor="#FFFF66"><div align="center">VEN</div></td>
    <td width="8%" bgcolor="#FFFF66"><div align="center">รหัสยา</div></td>
    <td width="5%" bgcolor="#FFFF66"><div align="center">&#3588;&#3591;&#3588;&#3621;&#3633;&#3591;</div></td>
    <td width="7%" bgcolor="#FFFF66"><div align="center">&#3627;&#3609;&#3656;&#3623;&#3618;</div></td>
    <td width="5%" bgcolor="#FFFF66"><div align="center">Location</div></td>
    <td width="9%" bgcolor="#FFFF66"><div align="center">Rate เดือนก่อน </div></td>
    <td width="7%" bgcolor="#FFFF66"><div align="center">&#3648;&#3627;&#3621;&#3639;&#3629;&#3651;&#3594;&#3657;&#3652;&#3604;&#3657; (&#3648;&#3604;&#3639;&#3629;&#3609;) </div></td>
  </tr>
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
   	  <%if C=0 then%>
  <tr bgcolor="#66FF66">
<%'  <tr bgcolor="#FD473E"> %>
     <%elseif C<0.5 then%>
  <tr bgcolor="#FF99FF">
       <%elseif C<=1 then%>
  <tr bgcolor="#99FFFF">
	  <%else%>
  <tr bgcolor="#66FF66">
	  <%end if%>  

    <td bgcolor="#FFFFCC">  <%response.write(runno)%>  . <a href="chkstock.asp?code=<%=(Drug.Fields.Item("WORKING_CODE").Value)%>"><%=(Drug.Fields.Item("drug_name").Value)%></a> <br />	<br /></td>
      <td bgcolor="#FFFFCC"><div align="center"><%=(Drug.Fields.Item("VEN").Value)%> </div></td>
      
    <td bgcolor="#FFFFCC"><div align="center"><span class="style2"> <%=(Drug.Fields.Item("WORKING_CODE").Value)%> </span></div>   </td>

	    <td bgcolor="#FFFFCC"><div align="center"><%=(Drug.Fields.Item("QTY_ON_HAND").Value)%></div></td>
	    <td bgcolor="#FFFFCC"><div align="center"><%=(Drug.Fields.Item("SALE_UNIT").Value)%></div></td>
	    <td bgcolor="#FFFFCC"><div align="center"><%=(Drug.Fields.Item("Location").Value)%></div></td>
	    <td bgcolor="#FFFFCC"><div align="center"><%=(Drug.Fields.Item("RATE_PER_MONTH").Value)%></div></td>
	    <td bgcolor="#FFFFCC">   <div align="center">
	      <%response.write(B)%>
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
<p>&nbsp;</p>

</body>
</html>
<%
Drug.Close()
Set Drug = Nothing
%>

