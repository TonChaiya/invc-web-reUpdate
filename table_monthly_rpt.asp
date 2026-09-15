<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Budget = request.querystring("FiscalYear")
mode = request.querystring("mode")

if mode = "" then 
	sql = "select * from (select R_S_STATUS+left(R_S_NUMBER,1) as MType, left(convert(char(8),OPERATE_DATE,112),6)+54300 as Mnth, card.[VALUE] as V from CARD where case 	when month(OPERATE_DATE)>=10 then Year(OPERATE_DATE)+543+1 else Year(OPERATE_DATE)+543 end ='" & Budget & "') " & _
		"as A PIVOT (sum(V) for MType in ([RO],[RR],[SS])) as pvt"
else 
	sql = "select * from (select R_S_STATUS+left(R_S_NUMBER,1) as MType, left(convert(char(8),OPERATE_DATE,112),6)+54300 as Mnth, card.[VALUE] as V from CARD inner join INV_MD on CARD.WORKING_CODE = INV_MD.WORKING_CODE where VMI is null and case 	when month(OPERATE_DATE)>=10 then Year(OPERATE_DATE)+543+1 else Year(OPERATE_DATE)+543 end ='" & Budget & "') " & _
		  "as A PIVOT (sum(V) for MType in ([RO],[RR],[SS])) as pvt"
end if 

Set PO = Server.CreateObject("ADODB.Recordset")
PO.Open sql, Conn, 1,3

Response.charset="windows-874"
%>



<table class="table table-bordered table-sm" width="100%" cellspacing="0">
  <thead>
  <tr>
    <th rowspan="2" valign="top"><div align="center">เดือน</div></th>
	<th colspan="3" valign="top"><div align="center">รับเข้าคลัง</div></th>
    <th rowspan="2" valign="top"><div align="center">จ่ายออกจากคลัง</div></th>
  </tr>
  <tr>
	<th><div align="center">รับจากการสั่งซื้อ</div></th>
    <th><div align="center">รับจากหน่วยงานอื่น</div></th>
	<th><div align="center">รวมมูลค่ารับ</div></th>
  </tr>
  </thead>
  <tbody>
  <%  runno = 1
While NOT PO.EOF
%>
    <tr> 
		<td><div align="center"><%=left(PO("Mnth"),4) & "-" & right(PO("Mnth"),2)%></div></td>
		<td><div align="center"><%=formatnumber(PO("RR"),2)%></div></td>
		<td><div align="center"><%=formatnumber(PO("RO"),2)%></div></td>
		<td><div align="center"><%=formatnumber(Csng(PO("RR")) + Csng(PO("RO")),2)%></div></td>
		<td><div align="center"><%=formatnumber(PO("SS"),2)%></div></td>	      
<tr>
   
<%
  
  runno = runno +1
  PO.MoveNext()
Wend
%>
	</tbody>
</table>