<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Budget = request.querystring("FiscalYear")
mode = request.querystring("mode")


if mode = "buy" then 
	sql = "SELECT INV_MD.WORKING_CODE, INV_MD.DRUG_NAME, INV_MD.DOSAGE_FORM, STD_RATIO3, DATE_ENTER, SALE_VALUE, SALE_QUAN, INV_MD.ABC, INV_MD.VEN,Y_1.V_1,Y_1.Q_1, SALE_VALUE-Y_1.V_1 as DiffValue, SALE_QUAN-Y_1.Q_1 as DiffQty, EDMAP FROM MBS_RE_Y INNER JOIN INV_MD ON INV_MD.WORKING_CODE=MBS_RE_Y.WORKING_CODE INNER JOIN TBLED_NED ON MBS_RE_Y.ED_NED=TBLED_NED.EDCODE INNER JOIN (SELECT WORKING_CODE, SALE_VALUE as V_1,SALE_QUAN as Q_1 FROM MBS_RE_Y WHERE [YEAR]+543='" & Budget-1 & "' ) as Y_1 on MBS_RE_Y.WORKING_CODE = Y_1.WORKING_CODE WHERE (([YEAR]+543='" & Budget & "') AND (INV_MD.NOUSE IS NULL  Or INV_MD.NOUSE='') AND (INV_MD.OUT_OF_LIST IS NULL Or INV_MD.OUT_OF_LIST='') AND VMI is null) ORDER BY SALE_VALUE-Y_1.V_1 desc"
else 
	sql = "SELECT INV_MD.WORKING_CODE, INV_MD.DRUG_NAME, INV_MD.DOSAGE_FORM, STD_RATIO3, DATE_ENTER, SALE_VALUE, SALE_QUAN, INV_MD.ABC, INV_MD.VEN,Y_1.V_1,Y_1.Q_1, SALE_VALUE-Y_1.V_1 as DiffValue, SALE_QUAN-Y_1.Q_1 as DiffQty, EDMAP FROM MBS_RE_Y INNER JOIN INV_MD ON INV_MD.WORKING_CODE=MBS_RE_Y.WORKING_CODE INNER JOIN TBLED_NED ON MBS_RE_Y.ED_NED=TBLED_NED.EDCODE INNER JOIN (SELECT WORKING_CODE, SALE_VALUE as V_1,SALE_QUAN as Q_1 FROM MBS_RE_Y WHERE [YEAR]+543='" & Budget-1 & "' ) as Y_1 on MBS_RE_Y.WORKING_CODE = Y_1.WORKING_CODE WHERE (([YEAR]+543='" & Budget & "') AND (INV_MD.NOUSE IS NULL  Or INV_MD.NOUSE='') AND (INV_MD.OUT_OF_LIST IS NULL Or INV_MD.OUT_OF_LIST='')) ORDER BY SALE_VALUE-Y_1.V_1 desc"
end if 

Set PO = Server.CreateObject("ADODB.Recordset")
PO.Open sql, Conn, 1,3

' if PO.EOF then 

	' sql = "select m.WORKING_CODE, DRUG_NAME, DOSAGE_FORM,STD_RATIO3,QTY_ON_HAND/std_ratio3 as REMAIN, SUM(ACTIVE_QTY1+ACTIVE_QTY2+ACTIVE_QTY3) As SALE_QUAN, Sum(c.[VALUE]) as SALE_VALUE, ABC,VEN from CARD c INNER JOIN INV_MD m ON c.WORKING_CODE=m.WORKING_CODE AND R_S_STATUS ='S' WHERE case 	when month(OPERATE_DATE)>=10 then Year(OPERATE_DATE)+543+1 else Year(OPERATE_DATE)+543 end ='" & Budget & "' group by m.WORKING_CODE, DRUG_NAME, DOSAGE_FORM,STD_RATIO3,QTY_ON_HAND/std_ratio3,ABC,VEN order by Sum(c.[VALUE]) desc"
	' Set PO = Server.CreateObject("ADODB.Recordset")
	' PO.Open sql, Conn, 1,3

' end if 

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
	<th ><div align="center">วันที่เข้า รพ.</div></th>
	<th><div align="center">NLEM</div></th>
	<th><div align="center">ABC</div></th>
	<th><div align="center">VEN</div></th>
    <th><div align="center">จำนวนปี <%=Budget-1%></div></th>
	<th><div align="center">มูลค่าปี <%=Budget-1%></div></th>
	<th><div align="center">จำนวนปี <%=Budget%></div></th>
	<th><div align="center">มูลค่าปี <%=Budget%></div></th>
	<th><div align="center">จำนวนที่ต่างกัน</div></th>
	<th><div align="center">มูลค่าที่ต่างกัน</div></th>
    
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
		<td><div align="right"><%=PO("DATE_ENTER")%></div></td>
	    <td><div align="center"><%=PO("EDMAP")%></div></td>
		<td><div align="center"><%=PO("ABC")%></div></td>
		<td><div align="center"><%=PO("VEN")%></div></td>
		<td><div align="right"><%=formatnumber(PO("Q_1"),2)%></div></td>
	    <td><div align="right"><%=formatnumber(PO("V_1"),2)%></div></td>
		<td><div align="right"><%=formatnumber(PO("SALE_QUAN"),2)%></div></td>
	    <td><div align="right"><%=formatnumber(PO("SALE_VALUE"),2)%></div></td>
	    <td><div align="right"><%=formatnumber(PO("DiffQty"),2)%></div></td>
	    <td><div align="right"><%=formatnumber(PO("DiffValue"),2)%></div></td>  
	<tr>
   
<%
  
  runno = runno +1
  PO.MoveNext()
Wend
%>
	</tbody>
</table>