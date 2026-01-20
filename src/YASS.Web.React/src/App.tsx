import { BrowserRouter, Routes, Route } from 'react-router-dom';
import RoomListPage from './pages/RoomListPage';
import RoomPlayerPage from './pages/RoomPlayerPage';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<RoomListPage />} />
        <Route path="/room/:roomId" element={<RoomPlayerPage />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
