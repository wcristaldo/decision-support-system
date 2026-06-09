import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import '../styles/UserManagement.css';

export default function UserManagement() {
  const [usuarios, setUsuarios] = useState([]);
  const [showResetModal, setShowResetModal] = useState(false);
  const [selectedUser, setSelectedUser] = useState(null);
  const [newPassword, setNewPassword] = useState('');
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState('');
  const navigate = useNavigate();

  useEffect(() => {
    fetchUsuarios();
  }, []);

  const fetchUsuarios = async () => {
    try {
      const response = await fetch('http://localhost:5000/api/usuarios', {
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        }
      });
      if (response.ok) {
        const data = await response.json();
        setUsuarios(data);
      }
    } catch (error) {
      console.error('Error:', error);
      setMessage('Error al cargar usuarios');
    } finally {
      setLoading(false);
    }
  };

  const handleResetPassword = async () => {
    if (!newPassword.trim()) {
      setMessage('Ingresa una nueva contraseña');
      return;
    }

    try {
      const response = await fetch(`http://localhost:5000/api/auth/reset-password/${selectedUser.id}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        },
        body: JSON.stringify({ newPassword })
      });

      if (response.ok) {
        setMessage('Contraseña reseteada correctamente');
        setShowResetModal(false);
        setNewPassword('');
        setSelectedUser(null);
        fetchUsuarios();
      } else {
        const error = await response.json();
        setMessage(error.message || 'Error al resetear contraseña');
      }
    } catch (error) {
      console.error('Error:', error);
      setMessage('Error al resetear contraseña');
    }
  };

  if (loading) return <div className="loading">Cargando usuarios...</div>;

  return (
    <div className="user-management">
      <div className="header">
        <h1>Gestión de Usuarios</h1>
        <button className="back-btn" onClick={() => navigate('/')}>← Volver</button>
      </div>

      {message && (
        <div className={`message ${message.includes('Error') ? 'error' : 'success'}`}>
          {message}
          <button onClick={() => setMessage('')}>✕</button>
        </div>
      )}

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>Nombre</th>
              <th>Email</th>
              <th>Activo</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {usuarios.map(usuario => (
              <tr key={usuario.id}>
                <td>{usuario.nombre}</td>
                <td>{usuario.email}</td>
                <td>{usuario.activo ? '✓' : '✗'}</td>
                <td>
                  <button
                    className="reset-btn"
                    onClick={() => {
                      setSelectedUser(usuario);
                      setShowResetModal(true);
                    }}
                  >
                    Resetear Contraseña
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showResetModal && (
        <div className="modal-overlay" onClick={() => setShowResetModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h2>Resetear Contraseña</h2>
            <p>Usuario: <strong>{selectedUser?.nombre}</strong></p>

            <input
              type="password"
              placeholder="Nueva contraseña"
              value={newPassword}
              onChange={e => setNewPassword(e.target.value)}
              onKeyPress={e => e.key === 'Enter' && handleResetPassword()}
              autoFocus
            />

            <div className="modal-buttons">
              <button className="cancel-btn" onClick={() => {
                setShowResetModal(false);
                setNewPassword('');
              }}>Cancelar</button>
              <button className="confirm-btn" onClick={handleResetPassword}>
                Resetear
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
