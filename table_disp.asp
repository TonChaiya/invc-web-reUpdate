<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Budget = request.querystring("FiscalYear")
mode = request.querystring("mode")
'flag = request.querystring("flag")

if mode = "buy" then
	sql = "SELECT INV_MD.WORKING_CODE, INV_MD.DRUG_NAME, INV_MD.DOSAGE_FORM, STD_RATIO3, QTY_ON_HAND/std_ratio3 as REMAIN, SALE_VALUE, SALE_QUAN, [YEAR]+543 as YY, INV_MD.ABC, INV_MD.VEN, INV_MD.NOUSE, INV_MD.OUT_OF_LIST, TBLED_NED.EDMAP FROM MBS_RE_Y INNER JOIN INV_MD ON INV_MD.WORKING_CODE=MBS_RE_Y.WORKING_CODE INNER JOIN TBLED_NED ON INV_MD.ED_NED=TBLED_NED.EDCODE WHERE (([YEAR]+543='" & Budget & "') AND (INV_MD.NOUSE IS NULL  Or INV_MD.NOUSE='') AND (INV_MD.OUT_OF_LIST IS NULL Or INV_MD.OUT_OF_LIST='') AND VMI is NULL) ORDER BY MBS_RE_Y.SALE_VALUE DESC"
else 	
	sql = "SELECT INV_MD.WORKING_CODE, INV_MD.DRUG_NAME, INV_MD.DOSAGE_FORM, STD_RATIO3, QTY_ON_HAND/std_ratio3 as REMAIN, SALE_VALUE, SALE_QUAN, [YEAR]+543 as YY, INV_MD.ABC, INV_MD.VEN, INV_MD.NOUSE, INV_MD.OUT_OF_LIST, TBLED_NED.EDMAP FROM MBS_RE_Y INNER JOIN INV_MD ON INV_MD.WORKING_CODE=MBS_RE_Y.WORKING_CODE INNER JOIN TBLED_NED ON INV_MD.ED_NED=TBLED_NED.EDCODE WHERE (([YEAR]+543='" & Budget & "') AND (INV_MD.NOUSE IS NULL  Or INV_MD.NOUSE='') AND (INV_MD.OUT_OF_LIST IS NULL Or INV_MD.OUT_OF_LIST='')) ORDER BY MBS_RE_Y.SALE_VALUE DESC"
end if 

Set PO = Server.CreateObject("ADODB.Recordset")
PO.Open sql, Conn, 1,3

if PO.EOF then 
	if mode = "buy" then 
		sql = "select m.WORKING_CODE, DRUG_NAME, DOSAGE_FORM,STD_RATIO3,QTY_ON_HAND/std_ratio3 as REMAIN, SUM(ACTIVE_QTY1+ACTIVE_QTY2+ACTIVE_QTY3) As SALE_QUAN, Sum(c.[VALUE]) as SALE_VALUE, ABC,VEN, e.EDMAP from CARD c INNER JOIN INV_MD m ON c.WORKING_CODE=m.WORKING_CODE AND R_S_STATUS ='S' INNER JOIN TBLED_NED e ON m.ED_NED=e.EDCODE  WHERE m.VMI is NULL and case when month(OPERATE_DATE)>=10 then Year(OPERATE_DATE)+543+1 else Year(OPERATE_DATE)+543 end ='" & Budget & "' group by m.WORKING_CODE, DRUG_NAME, DOSAGE_FORM,STD_RATIO3,QTY_ON_HAND/std_ratio3,ABC,VEN,EDMAP order by Sum(c.[VALUE]) desc"
	else 
		sql = "select m.WORKING_CODE, DRUG_NAME, DOSAGE_FORM,STD_RATIO3,QTY_ON_HAND/std_ratio3 as REMAIN, SUM(ACTIVE_QTY1+ACTIVE_QTY2+ACTIVE_QTY3) As SALE_QUAN, Sum(c.[VALUE]) as SALE_VALUE, ABC,VEN,e.EDMAP from CARD c INNER JOIN INV_MD m ON c.WORKING_CODE=m.WORKING_CODE AND R_S_STATUS ='S' INNER JOIN TBLED_NED e ON m.ED_NED=e.EDCODE WHERE case when month(OPERATE_DATE)>=10 then Year(OPERATE_DATE)+543+1 else Year(OPERATE_DATE)+543 end ='" & Budget & "' group by m.WORKING_CODE, DRUG_NAME, DOSAGE_FORM,STD_RATIO3,QTY_ON_HAND/std_ratio3,ABC,VEN,EDMAP order by Sum(c.[VALUE]) desc"
	end if 
	
	Set PO = Server.CreateObject("ADODB.Recordset")
	PO.Open sql, Conn, 1,3

end if 

Response.charset="windows-874"
%>



<table class="table table-bordered table-sm" width="100%" cellspacing="0">
  <thead>
  <tr>
    <th><div align="center">ลำดับ</div></th>
	<th><div align="center">รหัสยา</div></th>
    <th><div align="center">รายการยา/เวชภัณฑ์</div></th>
    <th><div align="center">รูปแบบ</div></th>
	<th><div align="center">ขนาดบรรจุ</div></th>
	<th ><div align="center">คงเหลือ</div></th>
    <th><div align="center">จำนวนที่ใช้</div></th>
	<th><div align="center">มูลค่าการใช้</div></th>
    <th><div align="center">ABC</div></th>
	<th><div align="center">VEN</div></th>
	<th><div align="center">NLEM</div></th>
  </tr>
  </thead>
  <tbody>
  <%  runno = 1
While NOT PO.EOF
%>
	
    <tr> 

		<td><div align="center"><%=runno%></div></td>
		<td><div align="center"><%=PO("WORKING_CODE")%></div></td>
		<td><div align="left"><%=(PO("DRUG_NAME"))%></div></td>
		<td><div align="center"><%=PO("DOSAGE_FORM")%></div></td>
		<td><div align="right"><%=PO("STD_RATIO3")%></div></td>
		<td><div align="right"><%=formatnumber(PO("REMAIN"),0)%></div></td>
	    <td><div align="right"><%=formatnumber(PO("SALE_QUAN"),2)%></div></td>
	    <td><div align="right"><%=formatnumber(PO("SALE_VALUE"),2)%></div></td>
	    <td><div align="center"><%=PO("ABC")%></div></td>
		<td><div align="center"><%=PO("VEN")%></div></td>
		<td><div align="center"><%=PO("EDMAP")%></div></td>
	      
<tr>
   
<%
  
  runno = runno +1
  PO.MoveNext()
Wend
%>
	</tbody>
</table>