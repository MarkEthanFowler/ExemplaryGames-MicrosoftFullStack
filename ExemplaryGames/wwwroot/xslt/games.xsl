<?xml version ="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:output method="html" indent="yes" />

	<xsl:template match="/">
		<html>
			<head>
				<title>Games Directory (XML/XSL)</title>
				<link rel="stylesheet" type="text/css" href="/css/styles.css" />
			</head>
			<body class="itemsStyle">
				<h1>Games Directory (XML/XSL)</h1>
				<table class="game-directory">
					<thread>
						<tr>
							<th>Title</th>
							<th>Price</th>
							<th>Condition</th>
							<th>Total Offers</th>
							<th>Max Offer</th>
						</tr>
					</thread>
					
					<xsl:for-each select="games/game">
						<tr>
							<td><xsl:value-of select="title"/></td>
							<td><xsl:value-of select="price"/></td>
							<td><xsl:value-of select="condition"/></td>
							<td><xsl:value-of select="totalOffers"/></td>
							<td><xsl:value-of select="maxOffer"/></td>
						</tr>
					</xsl:for-each>
				</table>
			</body>
		</html>
	</xsl:template>
	
</xsl:stylesheet>
