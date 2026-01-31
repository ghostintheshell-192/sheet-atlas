import React from 'react';
import { ScatterChart, Scatter, XAxis, YAxis, ZAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, Label } from 'recharts';

const CompetitorPositioningMap = () => {
  const competitors = [
    { x: 30, y: 85, z: 120, name: 'Microsoft Spreadsheet Compare', color: '#0088FE' },
    { x: 80, y: 20, z: 100, name: 'Synkronizer Excel Compare', color: '#00C49F' },
    { x: 60, y: 40, z: 90, name: 'Ablebits Compare Sheets', color: '#FFBB28' },
    { x: 20, y: 90, z: 80, name: 'SeekFast', color: '#FF8042' },
    { x: 50, y: 40, z: 70, name: 'PowerGREP', color: '#8884d8' },
    { x: 40, y: 30, z: 60, name: 'Excel Native Functions', color: '#82ca9d' },
    { x: 70, y: 75, z: 150, name: 'Excel Cross Reference Viewer', color: '#FF0000' },
  ];

  const renderTooltip = (props) => {
    const { payload } = props;
    if (payload && payload.length) {
      const data = payload[0].payload;
      return (
        <div className="bg-white p-2 border rounded shadow-md">
          <p className="font-bold">{data.name}</p>
          <p>Capacità ricerca cross-file: {data.x}%</p>
          <p>Capacità confronto avanzato: {data.y}%</p>
        </div>
      );
    }
    return null;
  };

  return (
    <div className="w-full h-96 p-4 bg-white rounded shadow">
      <h3 className="text-lg font-semibold text-center mb-4">Mappa di posizionamento competitivo</h3>
      <ResponsiveContainer width="100%" height="85%">
        <ScatterChart
          margin={{
            top: 20,
            right: 20,
            bottom: 40,
            left: 40,
          }}
        >
          <CartesianGrid />
          <XAxis type="number" dataKey="x" name="Capacità ricerca cross-file" unit="%" domain={[0, 100]}>
            <Label value="Capacità ricerca cross-file" position="bottom" style={{ textAnchor: 'middle' }} />
          </XAxis>
          <YAxis type="number" dataKey="y" name="Capacità confronto avanzato" unit="%" domain={[0, 100]}>
            <Label value="Capacità confronto avanzato" angle={-90} position="left" style={{ textAnchor: 'middle' }} />
          </YAxis>
          <ZAxis type="number" dataKey="z" range={[60, 200]} />
          <Tooltip content={renderTooltip} />
          <Legend />
          {competitors.map((competitor, index) => (
            <Scatter
              key={index}
              name={competitor.name}
              data={[competitor]}
              fill={competitor.color}
            />
          ))}
        </ScatterChart>
      </ResponsiveContainer>
    </div>
  );
};

export default CompetitorPositioningMap;
