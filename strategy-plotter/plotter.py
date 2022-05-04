import pandas as pd
import plotly.express as px
import plotly.graph_objects as go
from plotly.subplots import make_subplots
from pathlib import Path

# folderName = "2022-04-28T14.07.08-ALPHA-CMA-"

# inCsvPath = "C:\\Apps\\GitHub\\strategy-plotter\\strategy-plotter\\bin\\Debug\\net6.0\\results\\{}\\out-best.csv".format(folderName)
inCsvPath = "C:\\Apps\\GitHub\\strategy-plotter\\strategy-plotter\\bin\\Debug\\net6.0\\out.csv"
outHtmlPath = "C:\\Apps\\GitHub\\strategy-plotter\\strategy-plotter\\bin\\Debug\\net6.0\\plottedResults.html"

df = pd.read_csv(inCsvPath)

fig = make_subplots(rows=3, cols=1, shared_xaxes=True)

fig.add_trace(go.Scatter(x=df.index,y=df.trade,name='Trade',mode='lines',connectgaps=True, line={'color':'red'}),row=1,col=1)
fig.add_trace(go.Scatter(x=df.index,y=df.enter,name='Enter price',mode='lines',connectgaps=True, line={'color':'orange'}),row=1,col=1)
fig.add_trace(go.Scatter(x=df.index,y=df.price,name='Price',mode='lines',connectgaps=True, line={'color':'grey'}),row=1,col=1)

fig.add_trace(go.Scatter(x=df.index,y=df.lspread,name='Low Spread',mode='lines',connectgaps=True, line={'color':'magenta'}),row=1,col=1)
fig.add_trace(go.Scatter(x=df.index,y=df.hspread,name='High Spread',mode='lines',connectgaps=True, line={'color':'magenta'}),row=1,col=1)

fig.add_trace(go.Scatter(x=df.index,y=df.asset,name='Asset',mode='lines',connectgaps=True, line={'color':'purple'}),row=2,col=1)
fig.add_trace(go.Scatter(x=df.index,y=df.cost,name='Cost',mode='lines',connectgaps=True, line={'color':'red'}),row=2,col=1)

fig.add_trace(go.Scatter(x=df.index,y=df.currency,name='Currency',mode='lines',connectgaps=True, line={'color':'green'}),row=3,col=1)
fig.add_trace(go.Scatter(x=df.index,y=df.equity,name='Equity',mode='lines',connectgaps=True, line={'color':'grey'}),row=3,col=1)
fig.add_trace(go.Scatter(x=df.index,y=df['budget extra'],name='Budget Extra',mode='lines',connectgaps=True, line={'color':'darkgreen'}),row=3,col=1)

fig.update_traces(hovertemplate='%{y}')
fig.update_layout(title_text="Backtest", hovermode='x unified')

fig.write_html(outHtmlPath)