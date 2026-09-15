<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->

<%

Budget = request.querystring("FiscalYear")

sql = "select m.WORKING_CODE as WORKING_CODE_m, m.DRUG_NAME, m.SALE_UNIT, m.QTY_ON_HAND, m.TOTAL_COST, m.TOTAL_VALUE, m.STD_PRICE3, m.STD_RATIO3, m.RATE_PER_MONTH, b.* from INV_MD m left join (SELECT MAY_BUY, MAY_BUY*PACK_RATIO as Q_PLAN, P_UNIT_COST, MAY_BUY*P_UNIT_COST as V_PLAN, WORKING_CODE, (BD_1_R_QUAN + BD_2_R_QUAN + BD_3_R_QUAN + BD_4_R_QUAN) as Q_BUY, (BD_1_R_VALUE + BD_2_R_VALUE + BD_3_R_VALUE + BD_4_R_VALUE) as V_BUY FROM BUYPLAN WHERE BUYPLAN.[YEAR]='" & Budget & "') as b on m.WORKING_CODE = b.WORKING_CODE WHERE NOUSE is null order by DRUG_NAME"

Set Drug = Server.CreateObject("ADODB.Recordset")
Drug.Open sql, Conn, 1,3



Function CVNum2P(NUM,Dec)

	If IsNull(NUM) = True Or NUM = "" Then
		CVNum2P = 0
	Else
		If Dec ="Y" Then
			CVNum2P = formatnumber(NUM)
		else
			CVNum2P = formatnumber(NUM,"0,000")
		End If
	End If

End Function


Response.charset="windows-874"
%>



<table class="table table-bordered table-sm" width="100%" cellspacing="0">
  <thead>
  <tr>
    <th rowspan="2"><div align="center">รหัส</div></th>
    <th rowspan="2"><div align="center">ชื่อยา/เวชภัณฑ์</div></th>
	<th rowspan="2"><div align="center">จำนวนคงคลัง</div></th>
	<th rowspan="2"><div align="center">หน่วย</div></th>
	<th rowspan="2"><div align="center">มูลค่าคงคลัง</div></th>
	<th colspan="2"><div align="center">ซื้อล่าสุด</div></th>
    <th colspan="2"><div align="center">แผนจัดซื้อ </div></th>
    <th colspan="2"><div align="center">ซื้อแล้ว (จำนวน)</div></th>
    
  </tr>
  <tr>
    <th><div align="center">ราคา</div></th>
	<th><div align="center">ขนาดบรรจุ</div></th>
    <th><div align="center">จำนวน</div></th>
    <th><div align="center">มูลค่า</div></th>
    <th><div align="center">จำนวน</div></th>
    <th><div align="center">มูลค่า</div></th>
  </tr>
  </thead>
  <tbody>
  <%  runno = 1
While NOT Drug.EOF
%>
	
    <tr> 

		<td><div align="center"><%=Drug("WORKING_CODE_m")%></div></td>
		<td><div align="left"><%=Drug("DRUG_NAME")%></div></td>
		<td><div align="center"><%=Drug("QTY_ON_HAND")%></div></td>
		<td><div align="right"><%=Drug("SALE_UNIT")%></div></td>
		<td><div align="right"><%=CVNum2P(Drug("TOTAL_VALUE"),"Y")%></div></td>
	    <td><div align="right"><%=CVNum2P(Drug("STD_PRICE3"),"Y")%></div></td>
	    <td><div align="right"><%=CVNum2P(Drug("STD_RATIO3"),"N")%></div></td>
		<td><div align="right"><%=CVNum2P(Drug("Q_PLAN"),"N")%></div></td>
		<td><div align="right"><%=CVNum2P(Drug("V_PLAN"),"Y")%></div></td>
		<td><div align="right"><%=CVNum2P(Drug("Q_BUY"),"N")%></div></td>
		<td><div align="right"><%=CVNum2P(Drug("V_BUY"),"Y")%></div></td>
	    
	      
<tr>
   
<%
  
  runno = runno +1
  Drug.MoveNext()
Wend
%>
	</tbody>
</table>