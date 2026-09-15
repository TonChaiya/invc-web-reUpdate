<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>
<!--#include file="Connections/INVFlood.asp" -->

<%

SearchKey = request.querystring("SearchKey")
'SearchKey = "DIA"

sql = "select WORKING_CODE, DRUG_NAME, COMPOSITION, DOSAGE_FORM,STD_RATIO3 FROM INV_MD WHERE (WORKING_CODE+DRUG_NAME+COMPOSITION LIKE '%" & SearchKey & "%')"

Set objRec = Server.CreateObject("ADODB.Recordset")
objRec.Open sql, Conn, 1,3


Response.charset="windows-874"

%>


<table class="table table-bordered table-sm table-hover">
	<tr>
		<th>รหัส</th>
		<th>ชื่อยาและเวชภัณฑ์</th>
	</tr>

<%

While Not objRec.EOF
	
	
	'HN = Trim(objRec.Fields("hn").Value)
	'AN = Trim(objRec.Fields("ladmit_n").Value)
	'PNAME = Trim(objRec.Fields("firstName").Value) & " " & Trim(objRec.Fields("lastName").Value)
	
	
	'string json = "{\"PNAME\":\"&Trim(objRec.Fields("firstName").Value) & " " & Trim(objRec.Fields("lastName").Value)&\"}"
%>	
				
				<tr onclick="selectItemSearch('<%=objRec("WORKING_CODE")%>','<%=objRec("DRUG_NAME")%>')">
					<td class="text-primary"><%=objRec("WORKING_CODE")%></td>
					<td> <div class="text-primary"><%=objRec("DRUG_NAME")%> </div></td>
				</tr>
			
<%	
	Response.write(json)
	'Response.write( Trim(objRec.Fields("firstName").Value) & " " & Trim(objRec.Fields("lastName").Value) )
	objRec.MoveNext
Wend

objRec.Close()
Set objRec = Nothing
%>
</table>