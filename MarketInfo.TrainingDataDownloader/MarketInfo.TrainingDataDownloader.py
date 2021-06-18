import yfinance as yf
from datetime import date, timedelta
import pandas as pd
import sqlite3

SYMBOLS = [
	# Dow Jones
	'MMM',
	'AXP',
	'AMGN',
	'AAPL',
	'BA',
	'CAT',
	'CVX',
	'CSCO',
	'KO',
	'DOW',
	'GS',
	'HD',
	'HON',
	'IBM',
	'INTC',
	'JNJ',
	'JPM',
	'MCD',
	'MRK',
	'MSFT',
	'NKE',
	'PG',
	'CRM',
	'TRV',
	'UNH',
	'VZ',
	'V',
	'WBA',
	'WMT',
	'DIS',
]

NUM_DAYS = 10000
INTERVAL = '1d'

startDate = (date.today() - timedelta(NUM_DAYS))
endDate = date.today()

with sqlite3.connect('mltrainingdata.db') as conn:
	# Stock info goes into the 'StockInfo' table
	curs = conn.cursor()
	curs.execute("DROP TABLE IF EXISTS StockInfo")
	curs.execute("CREATE TABLE StockInfo (Ticker text, CONSTRAINT stocks_pk PRIMARY KEY (Ticker))")

	# Stock data goes into the 'StockPrices' table
	curs.execute("DROP TABLE IF EXISTS StockPrices")
	curs.execute("CREATE TABLE StockPrices (Date TIMESTAMP, Ticker TEXT, Open REAL, High REAL, Low REAL, Close REAL, Volume REAL, CONSTRAINT data_pk PRIMARY KEY (Date, Ticker))")

	print("Downloading data")
	data = yf.download(SYMBOLS, start=startDate, end=endDate, interval=INTERVAL, group_by='ticker')
	for symbol in SYMBOLS:
		print(f"Adding {symbol} data to database")

		curs.execute("INSERT INTO StockInfo VALUES (?)", (symbol,))

		sym_dat = data[(symbol,)]
	
		sym_dat = sym_dat.dropna()
		for timestamp, content in sym_dat.iterrows():
			curs.execute('INSERT INTO StockPrices VALUES (?,?,?,?,?,?,?)', (timestamp.date(), symbol, content['Close'], content['Open'], content['High'], content['Low'], content['Volume']))

	conn.commit()

